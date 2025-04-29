using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    /// <summary>
    ///     Simple download data impl
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task DownloadData(AbstractDownloadBase downloadFile, DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        var timeout = downloadSettings.Timeout;
        var client = downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName);
        var trials = downloadSettings.RetryCount == 0 ? 1 : downloadSettings.RetryCount;
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();
        var speedCalculator = new DownloadSpeedCalculator();

        while (downloadFile.RetryCount++ < trials)
        {
            var timeoutMs = timeout.TotalMilliseconds;
            using var cts = new CancellationTokenSource((int)Math.Min(timeoutMs * 5, timeoutMs * downloadFile.RetryCount));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.GetDownloadUrl());

                if (downloadSettings.Authentication != null)
                    request.Headers.Authorization = downloadSettings.Authentication;
                if (!string.IsNullOrEmpty(downloadSettings.Host))
                    request.Headers.Host = downloadSettings.Host;

                using var res =
                    await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                res.EnsureSuccessStatusCode();

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum);

                using var hashProvider = downloadSettings.GetCryptoTransform();

                Stream ms = hashCheckFile
                    ? MemoryStreamManager.GetStream()
                    : File.Create(filePath);
                var cryptoStream = new CryptoStream(ms, hashProvider, CryptoStreamMode.Write);

                // Reset speed calculator
                speedCalculator.Reset();

                await using (var stream = await res.Content.ReadAsStreamAsync(cts.Token))
                await using (var destStream = hashCheckFile ? cryptoStream : ms)
                {
                    using var buffer = MemoryPool<byte>.Shared.Rent(DefaultCopyBufferSize);

                    var bytesReadInTotal = 0L;
                    var lastProgressUpdateTime = DateTime.UtcNow;
                    var progressUpdateInterval = TimeSpan.FromMilliseconds(200); // 更新频率限制

                    while (true)
                    {
                        var bytesRead = await stream.ReadAsync(buffer.Memory, cts.Token);

                        if (bytesRead == 0) break;

                        bytesReadInTotal += bytesRead;

                        await destStream.WriteAsync(buffer.Memory[..bytesRead], cts.Token);

                        // 更新速度计算
                        var currentSpeed = speedCalculator.AddSample(bytesRead);

                        // 限制进度更新频率，避免UI过载
                        var now = DateTime.UtcNow;

                        if (!downloadSettings.ShowDownloadProgress ||
                            now - lastProgressUpdateTime < progressUpdateInterval)
                            continue;

                        lastProgressUpdateTime = now;
                        downloadFile.OnChanged(
                            currentSpeed,
                            ProgressValue.Create(bytesReadInTotal, responseLength),
                            bytesReadInTotal,
                            responseLength);
                    }

                    if (hashCheckFile && destStream is CryptoStream cStream)
                        await cStream.FlushFinalBlockAsync(cts.Token);

                    if (hashCheckFile)
                    {
                        var checkSum = Convert.ToHexString(hashProvider.Hash!.AsSpan());

                        if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // ReSharper disable once ConvertToUsingDeclaration
                        await using (var fs = File.Create(filePath))
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            await ms.CopyToAsync(fs, cts.Token);
                            await fs.FlushAsync(cts.Token);
                        }
                    }
                    else
                    {
                        await ms.FlushAsync(cts.Token);
                    }
                }

                await RecycleDownloadFile(downloadFile);

                // 使用最终速度作为完成回调的参数
                var finalSpeed = speedCalculator.TotalBytes / Stopwatch.GetElapsedTime(
                    Stopwatch.GetTimestamp() - (long)(timeout.TotalSeconds * Stopwatch.Frequency)
                ).TotalSeconds;

                downloadFile.RetryCount--;
                downloadFile.OnCompleted(true, null, finalSpeed);

                return;
            }
            catch (Exception e)
            {
                var delay = Math.Min(1000 * Math.Pow(2, downloadFile.RetryCount - 1), 10000);
                await Task.Delay((int)delay, CancellationToken.None);
                exceptions.Add(e);
            }
        }

        // We failed to download the file
        await RecycleDownloadFile(downloadFile);

        // We need to deduct 1 from the retry count because the last retry will not be counted
        downloadFile.RetryCount--;
        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }
}