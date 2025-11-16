using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    private record UrlInfo(long FileLength, bool SupportsRangeRequests);

    private const int MaxConcurrentChunkDownloads = 16;
    private const int ChunkDownloadBufferSize = 1024 * 1024; // 1 MB buffer

    private static bool CanUsePartialDownload(HttpResponseMessage res, long from, long to)
    {
        var parallelDownloadSupported =
            res.Content.Headers.ContentLength == to - from + 1 &&
            res.StatusCode == HttpStatusCode.PartialContent &&
            (res.Content.Headers.ContentRange?.HasRange ?? false);

        return parallelDownloadSupported;
    }

    private static async Task<UrlInfo?> CheckPartialDownloadSupportAsync(
        string url,
        DownloadSettings downloadSettings,
        CancellationToken ct)
    {
        var client = downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName);

        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            headReq.Headers.Range = new RangeHeaderValue(0, 0);

            if (downloadSettings.Authentication != null)
                headReq.Headers.Authorization = downloadSettings.Authentication;
            if (!string.IsNullOrEmpty(downloadSettings.Host))
                headReq.Headers.Host = downloadSettings.Host;

            using var headRes = await client
                .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);

            headRes.EnsureSuccessStatusCode();

            var supportsRangeRequests = CanUsePartialDownload(headRes, 0, 0);
            var fullLength = supportsRangeRequests
                ? headRes.Content.Headers.ContentRange?.Length ?? 0
                : headRes.Content.Headers.ContentLength ?? 0;

            return new UrlInfo(fullLength, supportsRangeRequests);
        }
        catch (HttpRequestException)
        {
            return new UrlInfo(0, false);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static IEnumerable<DownloadRange> CalculateDownloadRanges(
        long fileLength,
        long offset,
        int parts)
    {
        if (parts <= 1 || fileLength < MinimumChunkSize)
        {
            yield return new DownloadRange { Start = offset, End = offset + fileLength - 1 };
            yield break;
        }

        var partSize = fileLength / parts;
        var remainder = fileLength % parts;

        long currentStart = 0;

        for (var i = 0; i < parts; i++)
        {
            var currentPartSize = partSize + (i == parts - 1 ? remainder : 0);
            var start = currentStart;
            var end = start + currentPartSize - 1;

            yield return new DownloadRange
            {
                Start = start + offset,
                End = end + offset
            };

            currentStart += currentPartSize;
        }
    }

    /// <summary>
    /// Multipart download with smart retry, resume, and speed monitoring
    /// </summary>
    /// <param name="downloadFile">Download file information</param>
    /// <param name="downloadSettings">Download settings</param>
    /// <returns></returns>
    public static async Task MultiPartDownloadTaskAsync(
        AbstractDownloadBase? downloadFile,
        DownloadSettings downloadSettings)
    {
        if (downloadFile == null) return;

        var lxTempPath = GetTempDownloadPath();
        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        if (!Directory.Exists(downloadFile.DownloadPath))
            Directory.CreateDirectory(downloadFile.DownloadPath);

        // Fallback to normal download if parts <= 1
        if (downloadSettings.DownloadParts <= 1)
        {
            await DownloadData(downloadFile, downloadSettings);
            return;
        }

        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var downloadUrl = downloadFile.GetDownloadUrl();

        // Check if server supports partial downloads
        using var checkCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var urlInfo = await CheckPartialDownloadSupportAsync(downloadUrl, downloadSettings, checkCts.Token);

        // Fallback to normal download if not supported or file too small
        if (urlInfo is not { SupportsRangeRequests: true } || urlInfo.FileLength < MinimumChunkSize)
        {
            downloadFile.RetryCount = 0;
            await DownloadData(downloadFile, downloadSettings);
            return;
        }

        // Initialize for multipart download
        var globalSpeedCalculator = new DownloadSpeedCalculator();
        using var chunkManager = new ChunkManager(downloadSettings);
        var initialRanges = CalculateDownloadRanges(urlInfo.FileLength, 0, downloadSettings.DownloadParts).ToList();
        chunkManager.InitializeChunks(initialRanges);

        var exceptions = new List<Exception>();
        using var cts = new CancellationTokenSource(downloadSettings.Timeout);

        try
        {
            // Download all chunks with parallel workers
            var maxConcurrency = Math.Min(downloadSettings.DownloadThread, MaxConcurrentChunkDownloads);
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // Progress reporting task
            var progressReportingCts = new CancellationTokenSource();
            var progressTask = Task.Run(async () =>
            {
                while (!progressReportingCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(200, progressReportingCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!downloadSettings.ShowDownloadProgress) continue;

                    var totalBytes = chunkManager.GetTotalDownloadedBytes();
                    var currentSpeed = globalSpeedCalculator.CurrentSpeed;

                    downloadFile.OnChanged(
                        currentSpeed,
                        ProgressValue.Create(totalBytes, urlInfo.FileLength),
                        totalBytes,
                        urlInfo.FileLength);
                }
            }, progressReportingCts.Token);

            // Chunk download workers
            var workerTasks = new List<Task>();
            for (var i = 0; i < maxConcurrency; i++)
            {
                var workerTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (!chunkManager.TryGetNextChunk(out var range, out var chunkState))
                            break;

                        await semaphore.WaitAsync(cts.Token);

                        try
                        {
                            await DownloadChunkAsync(
                                range,
                                chunkState,
                                downloadUrl,
                                downloadSettings,
                                chunkManager,
                                globalSpeedCalculator,
                                cts.Token);

                            chunkManager.CompleteChunk(range, chunkState);
                        }
                        catch (Exception ex)
                        {
                            // Handle chunk failure
                            var canRetry = chunkManager.HandleChunkFailure(range, chunkState, canSplit: true);
                            if (!canRetry)
                            {
                                exceptions.Add(ex);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                }, cts.Token);

                workerTasks.Add(workerTask);
            }

            // Wait for all workers to complete
            await Task.WhenAll(workerTasks);

            // Stop progress reporting
            await progressReportingCts.CancelAsync();

            try
            {
                await progressTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Check if all chunks completed successfully
            if (!chunkManager.AreAllChunksCompleted() || exceptions.Count > 0)
            {
                throw new AggregateException("Some chunks failed to download", exceptions);
            }

            // Merge chunks and verify hash
            await MergeChunksAndVerifyAsync(
                chunkManager,
                filePath,
                downloadFile.CheckSum,
                downloadSettings,
                cts.Token);

            // Calculate final speed
            var finalSpeed = globalSpeedCalculator.Recalculate();

            downloadFile.OnCompleted(true, null, finalSpeed);
        }
        catch (OperationCanceledException)
        {
            downloadFile.RetryCount++;

            if (downloadFile.RetryCount < downloadSettings.RetryCount || downloadSettings.RetryCount <= 0)
            {
                // Retry with backoff
                var delay = CalculateRetryDelay(downloadFile.RetryCount);
                await Task.Delay(delay, CancellationToken.None);

                // Retry entire multipart download
                await MultiPartDownloadTaskAsync(downloadFile, downloadSettings);
            }
            else
            {
                // Fallback to normal download
                downloadFile.RetryCount = 0;
                await DownloadData(downloadFile, downloadSettings);
            }
        }
        catch (Exception ex)
        {
            // On any failure, fallback to normal download
            downloadFile.RetryCount = 0;
            exceptions.Add(ex);

            await DownloadData(downloadFile, downloadSettings);
        }
    }

    /// <summary>
    ///     Download a single chunk with smart retry logic
    /// </summary>
    private static async Task DownloadChunkAsync(
        DownloadRange range,
        ChunkDownloadState chunkState,
        string downloadUrl,
        DownloadSettings downloadSettings,
        ChunkManager chunkManager,
        DownloadSpeedCalculator globalSpeedCalculator,
        CancellationToken ct)
    {
        var client = downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName);
        var tempFilePath = GetTempFilePath();
        chunkState.CreateTempFile(tempFilePath);

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);

        if (downloadSettings.Authentication != null)
            request.Headers.Authorization = downloadSettings.Authentication;
        if (!string.IsNullOrEmpty(downloadSettings.Host))
            request.Headers.Host = downloadSettings.Host;

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode || !CanUsePartialDownload(response, range.Start, range.End))
        {
            throw new HttpRequestException($"Failed to download chunk {range.Start}-{range.End}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = MemoryPool<byte>.Shared.Rent(ChunkDownloadBufferSize);

        var lastSpeedCheckTime = Stopwatch.GetTimestamp();
        var speedCheckIntervalTicks = 3 * Stopwatch.Frequency; // Check every 3 seconds

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.Memory, ct);
            if (bytesRead == 0) break;

            await chunkState.TempFileStream!.WriteAsync(buffer.Memory[..bytesRead], ct);

            globalSpeedCalculator.AddSample(bytesRead);

            // Check if download speed is too slow
            var now = Stopwatch.GetTimestamp();

            if (now - lastSpeedCheckTime <= speedCheckIntervalTicks) continue;

            lastSpeedCheckTime = now;

            if (!chunkState.IsTooSlow()) continue;

            // Cancel this chunk and retry
            chunkManager.HandleSlowChunk(range, chunkState);
            throw new OperationCanceledException("Chunk download too slow, retrying");
        }

        // Verify chunk completion
        if (chunkState.BytesDownloaded != range.Length)
        {
            throw new InvalidOperationException(
                $"Chunk incomplete: expected {range.Length}, got {chunkState.BytesDownloaded}");
        }

        await chunkState.TempFileStream!.FlushAsync(ct);
    }

    /// <summary>
    ///     Merge downloaded chunks and verify hash
    /// </summary>
    private static async Task MergeChunksAndVerifyAsync(
        ChunkManager chunkManager,
        string filePath,
        string? expectedCheckSum,
        DownloadSettings downloadSettings,
        CancellationToken ct)
    {
        var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(expectedCheckSum);
        using var hashProvider = downloadSettings.GetCryptoTransform();

        Stream outputStream = hashCheckFile
            ? MemoryStreamManager.GetStream()
            : File.Create(filePath);

        try
        {
            // Use CryptoStream with leaveOpen = true to prevent it from disposing outputStream
            var cryptoStream = hashCheckFile
                ? new CryptoStream(outputStream, hashProvider, CryptoStreamMode.Write, leaveOpen: true)
                : null;

            try
            {
                var destStream = cryptoStream ?? outputStream;

                foreach (var chunkState in chunkManager.GetCompletedChunksInOrder())
                {
                    if (chunkState.TempFileStream == null) continue;

                    chunkState.TempFileStream.Seek(0, SeekOrigin.Begin);
                    await chunkState.TempFileStream.CopyToAsync(destStream, ct);
                }

                if (cryptoStream != null)
                    await cryptoStream.FlushFinalBlockAsync(ct);

                await destStream.FlushAsync(ct);
            }
            finally
            {
                if (cryptoStream != null)
                    await cryptoStream.DisposeAsync();
            }

            // Verify hash if needed
            if (hashCheckFile && !string.IsNullOrEmpty(expectedCheckSum))
            {
                var checkSum = Convert.ToHexString(hashProvider.Hash!.AsSpan());

                if (!checkSum.Equals(expectedCheckSum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Hash mismatch: expected {expectedCheckSum}, got {checkSum}");
                }

                // Hash verified, write to final file
                await using var fs = File.Create(filePath);
                outputStream.Seek(0, SeekOrigin.Begin);
                await outputStream.CopyToAsync(fs, ct);
                await fs.FlushAsync(ct);
            }
        }
        finally
        {
            await outputStream.DisposeAsync();
        }
    }
}