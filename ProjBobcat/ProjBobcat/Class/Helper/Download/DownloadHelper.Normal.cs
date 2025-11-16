using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
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
    ///     Simple single-part download implementation with improved error handling
    /// </summary>
    /// <param name="downloadFile">Download file information</param>
    /// <param name="downloadSettings">Download settings</param>
    /// <returns></returns>
    public static async Task DownloadData(AbstractDownloadBase downloadFile, DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        if (!Directory.Exists(downloadFile.DownloadPath))
            Directory.CreateDirectory(downloadFile.DownloadPath);

        var timeout = downloadSettings.Timeout;
        var client = downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName);
        var trials = downloadSettings.RetryCount <= 0 ? 1 : downloadSettings.RetryCount;
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();
        var speedCalculator = new DownloadSpeedCalculator();

        while (downloadFile.RetryCount < trials)
        {
            using var cts = new CancellationTokenSource(timeout);
            Stream? tempFileStream = null;
            var tempFilePath = string.Empty;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.GetDownloadUrl());

                if (downloadSettings.Authentication != null)
                    request.Headers.Authorization = downloadSettings.Authentication;
                if (!string.IsNullOrEmpty(downloadSettings.Host))
                    request.Headers.Host = downloadSettings.Host;

                using var res = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                res.EnsureSuccessStatusCode();

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum);

                using var hashProvider = downloadSettings.GetCryptoTransform();

                // Use temp file for downloading, then move to final location
                tempFilePath = hashCheckFile ? string.Empty : GetTempFilePath();

                var ms = hashCheckFile
                    ? MemoryStreamManager.GetStream()
                    : (tempFileStream = File.Create(tempFilePath));

                try
                {
                    // Use CryptoStream with leaveOpen = true to prevent disposing ms
                    var cryptoStream = hashCheckFile
                        ? new CryptoStream(ms, hashProvider, CryptoStreamMode.Write, leaveOpen: true)
                        : null;

                    // Reset speed calculator
                    speedCalculator.Reset();

                    try
                    {
                        await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
                        using var buffer = MemoryPool<byte>.Shared.Rent(DefaultCopyBufferSize);

                        var destStream = cryptoStream ?? ms;
                        var bytesReadInTotal = 0L;
                        var lastProgressUpdateTime = Stopwatch.GetTimestamp();
                        var progressUpdateIntervalTicks = (long)(0.2 * Stopwatch.Frequency); // 200ms

                        while (true)
                        {
                            var bytesRead = await stream.ReadAsync(buffer.Memory, cts.Token);

                            if (bytesRead == 0) break;

                            bytesReadInTotal += bytesRead;

                            await destStream.WriteAsync(buffer.Memory[..bytesRead], cts.Token);

                            // Update speed calculation
                            var currentSpeed = speedCalculator.AddSample(bytesRead);

                            // Throttle progress updates to avoid UI overload
                            var now = Stopwatch.GetTimestamp();

                            if (downloadSettings.ShowDownloadProgress &&
                                now - lastProgressUpdateTime >= progressUpdateIntervalTicks)
                            {
                                lastProgressUpdateTime = now;
                                downloadFile.OnChanged(
                                    currentSpeed,
                                    ProgressValue.Create(bytesReadInTotal, responseLength),
                                    bytesReadInTotal,
                                    responseLength);
                            }
                        }

                        // Flush crypto stream if used
                        if (cryptoStream != null)
                            await cryptoStream.FlushFinalBlockAsync(cts.Token);

                        await destStream.FlushAsync(cts.Token);
                    }
                    finally
                    {
                        if (cryptoStream != null)
                            await cryptoStream.DisposeAsync();
                    }

                    // Handle hash verification
                    if (hashCheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                    {
                        var checkSum = Convert.ToHexString(hashProvider.Hash!.AsSpan());

                        if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                        {
                            // Hash mismatch, retry
                            downloadFile.RetryCount++;

                            var delay = CalculateRetryDelay(downloadFile.RetryCount);
                            await Task.Delay(delay, CancellationToken.None);
                            continue;
                        }

                        // Hash verified, write to final file
                        await using var fs = File.Create(filePath);

                        ms.Seek(0, SeekOrigin.Begin);
                        await ms.CopyToAsync(fs, cts.Token);
                        await fs.FlushAsync(cts.Token);
                    }
                    else if (!hashCheckFile)
                    {
                        // Close temp file stream
                        if (tempFileStream != null)
                        {
                            await tempFileStream.DisposeAsync();
                            tempFileStream = null;
                        }

                        // Move temp file to final location
                        if (File.Exists(filePath))
                            File.Delete(filePath);

                        File.Move(tempFilePath, filePath);
                        tempFilePath = string.Empty;
                    }
                }
                finally
                {
                    if (hashCheckFile)
                        await ms.DisposeAsync();
                    // tempFileStream will be disposed in the catch blocks
                }

                // Calculate final speed
                var finalSpeed = speedCalculator.Recalculate();

                downloadFile.OnCompleted(true, null, finalSpeed);

                return;
            }
            catch (HttpRequestException e)
            {
                downloadFile.RetryCount++;
                exceptions.Add(e);

                // Clean up temp file
                CleanupTempFile(tempFileStream, tempFilePath);

                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    // Don't retry on 404
                    break;
                }

                var delay = CalculateRetryDelay(downloadFile.RetryCount);
                await Task.Delay(delay, CancellationToken.None);
            }
            catch (OperationCanceledException e)
            {
                downloadFile.RetryCount++;
                exceptions.Add(e);

                // Clean up temp file
                CleanupTempFile(tempFileStream, tempFilePath);

                var delay = CalculateRetryDelay(downloadFile.RetryCount);
                await Task.Delay(delay, CancellationToken.None);
            }
            catch (Exception e)
            {
                downloadFile.RetryCount++;
                exceptions.Add(e);

                // Clean up temp file
                CleanupTempFile(tempFileStream, tempFilePath);

                var delay = CalculateRetryDelay(downloadFile.RetryCount);
                await Task.Delay(delay, CancellationToken.None);
            }
        }

        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }

    private static void CleanupTempFile(Stream? stream, string tempFilePath)
    {
        try
        {
            stream?.Dispose();

            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}