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
    private static HttpClient Data => HttpClientHelper.DataClient;

    /// <summary>
    ///     Simple download data impl
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task DownloadData(AbstractDownloadBase downloadFile, DownloadSettings? downloadSettings = null)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        downloadSettings ??= DownloadSettings.Default;

        var trials = downloadSettings.RetryCount == 0 ? 1 : downloadSettings.RetryCount;
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();

        for (var i = 0; i < trials; i++)
        {
            using var cts = new CancellationTokenSource(downloadSettings.Timeout * Math.Max(1, i + 1));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.GetDownloadUrl());

                if (downloadSettings.Authentication != null)
                    request.Headers.Authorization = downloadSettings.Authentication;
                if (!string.IsNullOrEmpty(downloadSettings.Host))
                    request.Headers.Host = downloadSettings.Host;

                using var res =
                    await Data.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                res.EnsureSuccessStatusCode();

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum);

                using var hashProvider = downloadSettings.GetCryptoTransform();

                var averageSpeed = 0d;
                var outputStream = File.Create(filePath);
                var cryptoStream = new CryptoStream(outputStream, hashProvider, CryptoStreamMode.Write, true);

                await using (var stream = await res.Content.ReadAsStreamAsync(cts.Token))
                await using (Stream destStream = hashCheckFile ? cryptoStream : outputStream)
                {
                    using var buffer = MemoryPool<byte>.Shared.Rent(DefaultCopyBufferSize);

                    var bytesReadInTotal = 0L;

                    while (true)
                    {
                        var startTime = Stopwatch.GetTimestamp();
                        var bytesRead = await stream.ReadAsync(buffer.Memory, cts.Token);

                        if (bytesRead == 0) break;

                        bytesReadInTotal += bytesRead;

                        await destStream.WriteAsync(buffer.Memory[..bytesRead], cts.Token);

                        var duration = Stopwatch.GetElapsedTime(startTime);
                        var elapsedTime = duration.TotalSeconds < 0.0001 ? 1 : duration.TotalSeconds;

                        averageSpeed = responseLength / elapsedTime;

                        if (downloadSettings.ShowDownloadProgress)
                            downloadFile.OnChanged(
                                averageSpeed,
                                ProgressValue.Create(bytesReadInTotal, responseLength),
                                responseLength,
                                responseLength);
                    }

                    if (hashCheckFile && destStream is CryptoStream cStream)
                        await cStream.FlushFinalBlockAsync(cts.Token);
                }

                if (hashCheckFile)
                {
                    var checkSum = Convert.ToHexString(hashProvider.Hash!.AsSpan());

                    if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadFile.RetryCount++;
                        FileHelper.DeleteFileWithRetry(filePath);
                        continue;
                    }
                }

                await RecycleDownloadFile(downloadFile);
                downloadFile.OnCompleted(true, null, averageSpeed);

                return;
            }
            catch (Exception e)
            {
                await Task.Delay(250, cts.Token);

                downloadFile.RetryCount++;
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