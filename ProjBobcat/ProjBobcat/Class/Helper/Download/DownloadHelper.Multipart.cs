﻿using System;
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
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Exceptions;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    private const double DefaultChunkSplitThreshold = 1.8;
    private const int MinimumChunkSize = 8192;

    /// <summary>
    ///     Receive data from remote stream (only for partial download)
    /// </summary>
    /// <returns>Elapsed time in seconds</returns>
    private static async Task<(double Speed, long BytesReceived)> ReceiveFromRemoteStreamAsync(
        Stream remoteStream,
        Stream destStream,
        DownloadSpeedCalculator speedCalculator,
        CancellationToken ct)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(MinimumChunkSize);
        var finalSpeed = 0d;
        var totalBytesReceived = 0L;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var bytesRead = await remoteStream.ReadAsync(buffer.Memory, ct);

            if (bytesRead == 0) break;

            totalBytesReceived += bytesRead;
            await destStream.WriteAsync(buffer.Memory[..bytesRead], ct);

            // 计算速度
            finalSpeed = speedCalculator.AddSample(bytesRead);
        }

        return (finalSpeed, totalBytesReceived);
    }

    private static async Task<(long FileLength, bool CanPartialDownload)?> CanUsePartialDownload(
        string url,
        DownloadSettings downloadSettings,
        CancellationToken ct)
    {
        var client = downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName);

        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);

            if (downloadSettings.Authentication != null)
                headReq.Headers.Authorization = downloadSettings.Authentication;
            if (!string.IsNullOrEmpty(downloadSettings.Host))
                headReq.Headers.Host = downloadSettings.Host;

            using var headRes = await client.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);

            headRes.EnsureSuccessStatusCode();

            var responseLength = headRes.Content.Headers.ContentLength ?? 0;
            var hasAcceptRanges = headRes.Headers.AcceptRanges.Count != 0;

            using var rangeGetMessage = new HttpRequestMessage(HttpMethod.Get, url);
            rangeGetMessage.Headers.Range = new RangeHeaderValue(0, 0);

            if (downloadSettings.Authentication != null)
                rangeGetMessage.Headers.Authorization = downloadSettings.Authentication;
            if (!string.IsNullOrEmpty(downloadSettings.Host))
                rangeGetMessage.Headers.Host = downloadSettings.Host;

            using var rangeGetRes =
                await client.SendAsync(rangeGetMessage, HttpCompletionOption.ResponseHeadersRead, ct);

            var parallelDownloadSupported =
                responseLength != 0 &&
                hasAcceptRanges &&
                rangeGetRes.StatusCode == HttpStatusCode.PartialContent &&
                (rangeGetRes.Content.Headers.ContentRange?.HasRange ?? false) &&
                rangeGetRes.Content.Headers.ContentLength == 1;

            return (responseLength, parallelDownloadSupported);
        }
        catch (HttpRequestException)
        {
            return (0, false);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static IEnumerable<DownloadRange> CalculateDownloadRanges(
        long fileLength,
        long offset,
        DownloadSettings downloadSettings)
    {
        var partSize = fileLength / downloadSettings.DownloadParts;
        var totalSize = fileLength;

        while (totalSize > 0)
        {
            //计算分片
            var to = totalSize;
            var from = totalSize - partSize;

            if (from < 0) from = 0;

            totalSize -= partSize;

            yield return new DownloadRange
            {
                Start = from + offset,
                End = to + offset,
                TempFileName = GetTempFilePath()
            };
        }
    }

    /// <summary>
    ///     分片下载方法（异步）
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task MultiPartDownloadTaskAsync(
        AbstractDownloadBase? downloadFile,
        DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        if (downloadFile == null) return;

        if (downloadSettings.DownloadParts <= 1)
        {
            // Fallback to normal download
            await DownloadData(downloadFile, downloadSettings);
            return;
        }

        var exceptions = new List<Exception>();
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var timeout = downloadSettings.Timeout;
        var trials = downloadSettings.RetryCount <= 0 ? 1 : downloadSettings.RetryCount;
        var subChunkSplitCount = 0;

        // 创建全局速度计算器，用于汇总所有分片下载速度
        var globalSpeedCalculator = new DownloadSpeedCalculator();
        var bytesReceived = 0L;
        var lastProgressUpdateTime = DateTime.UtcNow;
        var progressUpdateInterval = TimeSpan.FromMilliseconds(200); // 限制更新频率

        while (downloadFile.RetryCount++ < trials)
        {
            var timeoutMs = timeout.TotalMilliseconds;
            using var cts = new CancellationTokenSource((int)Math.Min(timeoutMs * 5, timeoutMs * downloadFile.RetryCount));

            try
            {
                if (downloadFile.Ranges == null ||
                    downloadFile.Ranges.IsEmpty ||
                    downloadFile.UrlInfo == null ||
                    downloadFile.UrlInfo.FileLength == 0)
                {
                    downloadFile.FinishedRangeStreams.Clear();

                    #region Get file size

                    using var partialDownloadCheckCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    (long FileLength, bool CanPartialDownload)? rawUrlInfo = null;

                    try
                    {
                        rawUrlInfo = await CanUsePartialDownload(
                            downloadFile.GetDownloadUrl(),
                            downloadSettings,
                            partialDownloadCheckCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Do nothing
                    }

                    // If rawUrlInfo == null, means the request is timeout and canceled
                    // If PartialDownloadRetryCount is greater than half of the total reties, fallback to slow download
                    if (!rawUrlInfo.HasValue)
                    {
                        downloadFile.PartialDownloadRetryCount++;

                        // If the retry count is 1 or condition met, we will fall back to normal download
                        if (downloadSettings.RetryCount == 0 ||
                            downloadFile.PartialDownloadRetryCount >= downloadSettings.RetryCount / 2)
                        {
                            // Reset the retry count
                            downloadFile.RetryCount = 0;

                            await DownloadData(downloadFile, downloadSettings);
                            return;
                        }

                        // Otherwise, we will retry the partial download
                        continue;
                    }

                    var urlInfo = rawUrlInfo.Value;

                    if (!urlInfo.CanPartialDownload)
                    {
                        // Reset the retry count
                        downloadFile.RetryCount = 0;

                        await DownloadData(downloadFile, downloadSettings);
                        return;
                    }

                    #endregion

                    var ranges = CalculateDownloadRanges(urlInfo.FileLength, 0, downloadSettings);

                    downloadFile.UrlInfo = new UrlInfo(urlInfo.FileLength, urlInfo.CanPartialDownload);
                    downloadFile.Ranges = [];

                    foreach (var range in ranges)
                        downloadFile.Ranges.TryAdd(range, null);
                }

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);

                #region Parallel download

                var requestCreationBlock =
                    new TransformBlock<PreChunkInfo, ChunkInfo?>(
                        async preChunkInfo =>
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, preChunkInfo.DownloadUrl);

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            request.Headers.Range =
                                new RangeHeaderValue(preChunkInfo.Range.Start, preChunkInfo.Range.End);

                            try
                            {
                                var downloadTask = await preChunkInfo.Client.SendAsync(
                                    request,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    preChunkInfo.Cts.Token);

                                if (!downloadTask.IsSuccessStatusCode ||
                                    !downloadTask.Content.Headers.ContentLength.HasValue ||
                                    downloadTask.Content.Headers.ContentLength == 0)
                                    // Some mirror will return non-200 code during the high load
                                    throw new HttpRequestException(
                                        $"Failed to download part {preChunkInfo.Range.Start}-{preChunkInfo.Range.End}, status code: {downloadTask.StatusCode}");

                                return new ChunkInfo(
                                    preChunkInfo.CurrentChunkSplitCount,
                                    downloadTask,
                                    preChunkInfo.Range,
                                    preChunkInfo.Cts);
                            }
                            catch (HttpRequestException e)
                            {
                                Debug.WriteLine(e);

                                throw;
                            }
                        }, new ExecutionDataflowBlockOptions
                        {
                            EnsureOrdered = false,
                            MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                            CancellationToken = cts.Token
                        });

                var threshold = (int)Math.Ceiling(downloadSettings.DownloadParts * DefaultChunkSplitThreshold);
                var downloadActionBlock = new ActionBlock<ChunkInfo?>(async chunkInfo =>
                {
                    if (chunkInfo == null) return;

                    var chunkCts = chunkInfo.CurrentChunkSplitCount < threshold
                        ? new CancellationTokenSource(timeout)
                        : chunkInfo.Cts;

                    using var res = chunkInfo.Response;

                    // We'll store the written file to a temp file stream, and keep its ref for the future use.
                    var fileToWriteTo = File.Create(chunkInfo.Range.TempFileName);
                    double speed;
                    long receivedBytes;

                    try
                    {
                        await using var stream = await res.Content.ReadAsStreamAsync(chunkInfo.Cts.Token);

                        // Here we are using chunkCts instead of the main cts
                        (speed, receivedBytes) = await ReceiveFromRemoteStreamAsync(
                            stream,
                            fileToWriteTo,
                            globalSpeedCalculator, // 使用全局速度计算器
                            chunkCts.Token);

                        ArgumentOutOfRangeException.ThrowIfNotEqual(fileToWriteTo.Length,
                            chunkInfo.Response.Content.Headers.ContentLength ?? 0);

                        downloadFile.FinishedRangeStreams.AddOrUpdate(chunkInfo.Range, fileToWriteTo, (_, oldStream) =>
                        {
                            try
                            {
                                oldStream.Dispose();
                            }
                            catch (Exception e)
                            {
                                // Do nothing because we don't care about the exception
                                Debug.WriteLine(e);
                            }

                            return fileToWriteTo;
                        });
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Size check failed, fast retry now
                        // Dispose the file stream, and assign a new temp file path
                        await fileToWriteTo.DisposeAsync();

                        var regeneratedTempFileInfo = chunkInfo.Range with { TempFileName = GetTempFilePath() };

                        ArgumentOutOfRangeException.ThrowIfEqual(downloadFile.Ranges.TryRemove(chunkInfo.Range, out _),
                            false);
                        ArgumentOutOfRangeException.ThrowIfEqual(
                            downloadFile.Ranges.TryAdd(regeneratedTempFileInfo, null), false);

                        throw;
                    }
                    catch (Exception e)
                    {
                        // If we end up to here, which means either the chunk is too large or the download speed is too slow
                        // So we need to further split the chunk into smaller parts

                        // If we already used all split trails, we just throw directly.
                        if (chunkInfo.CurrentChunkSplitCount >= threshold)
                            throw;

                        // If the chunk is small enough, we don't need to split it
                        if (chunkInfo.Range.End - chunkInfo.Range.Start <= MinimumChunkSize)
                            throw new DownloadChunkSplitException(false, e);

                        // Remove the current range from the download queue
                        ArgumentOutOfRangeException.ThrowIfEqual(downloadFile.Ranges.TryRemove(chunkInfo.Range, out _),
                            false);

                        var chunkLength = chunkInfo.Range.End - chunkInfo.Range.Start;
                        var subChunkRanges =
                            CalculateDownloadRanges(chunkLength, chunkInfo.Range.Start, downloadSettings);

                        // We add the sub chunks to the download queue
                        foreach (var range in subChunkRanges)
                            // Update the original range list
                            ArgumentOutOfRangeException.ThrowIfEqual(downloadFile.Ranges.TryAdd(range, null), false);

                        throw new DownloadChunkSplitException(true, e);
                    }
                    finally
                    {
                        if (chunkInfo.CurrentChunkSplitCount < threshold)
                            // If the sub chunk split count is greater than the max split count, we need to cancel the main cts
                            chunkCts.Dispose();
                    }

                    // 更新总下载字节数
                    var addedBytesReceived = Interlocked.Add(ref bytesReceived, receivedBytes);

                    // 限制进度更新频率，避免UI过载
                    var now = DateTime.UtcNow;
                    if (downloadSettings.ShowDownloadProgress &&
                        now - lastProgressUpdateTime >= progressUpdateInterval)
                    {
                        lastProgressUpdateTime = now;

                        // 使用当前全局速度计算器的速度
                        // 使用原子操作获取当前速度，避免线程竞争问题
                        var currentGlobalSpeed = Interlocked.Exchange(ref speed, 0);

                        downloadFile.OnChanged(
                            currentGlobalSpeed,
                            ProgressValue.Create(addedBytesReceived, downloadFile.UrlInfo.FileLength),
                            addedBytesReceived,
                            downloadFile.UrlInfo.FileLength);
                    }
                }, new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = false,
                    MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                var bufferBlock = new BufferBlock<PreChunkInfo>(new DataflowBlockOptions { EnsureOrdered = false });

                bufferBlock.LinkTo(requestCreationBlock, linkOptions);
                requestCreationBlock.LinkTo(downloadActionBlock, linkOptions);

                // Acquire the download URL
                var downloadUrl = downloadFile.GetDownloadUrl();

                foreach (var range in downloadFile.GetUndoneRanges())
                {
                    var chunkInfo = new PreChunkInfo(
                        downloadSettings.HttpClientFactory.CreateClient(DefaultDownloadClientName),
                        subChunkSplitCount,
                        downloadUrl,
                        range,
                        cts);

                    await bufferBlock.SendAsync(chunkInfo, cts.Token);
                }

                bufferBlock.Complete();
                await downloadActionBlock.Completion;

                if (!downloadFile.IsDownloadFinished())
                    continue;

                var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum);

                using var hashProvider = downloadSettings.GetCryptoTransform();

                Stream ms = hashCheckFile
                    ? MemoryStreamManager.GetStream(
#if NET9_0_OR_GREATER
                        Guid.CreateVersion7(),
#else
                        Guid.NewGuid(),
#endif
                        null,
                        downloadFile.FinishedRangeStreams.Sum(p => p.Value.Length))
                    : File.Create(filePath);
                var hashStream = new CryptoStream(ms, hashProvider, CryptoStreamMode.Write);

                await using (var destStream = hashCheckFile ? hashStream : ms)
                {
                    var index = 0;

                    foreach (var inputFileStream in downloadFile.GetFinishedStreamsInorder())
                    {
                        // Reset the stream position
                        inputFileStream.Seek(0, SeekOrigin.Begin);

                        // Because the feature of HTTP range response,
                        // the first byte of the first range is the last byte of the file.
                        // So we need to skip the first byte of the first range.
                        // (Expect the first part)
                        if (index != 0)
                            inputFileStream.Seek(1, SeekOrigin.Begin);

                        await inputFileStream.CopyToAsync(destStream, cts.Token);

                        index++;
                    }

                    await destStream.FlushAsync(cts.Token);

                    if (hashCheckFile && destStream is CryptoStream cStream)
                        await cStream.FlushFinalBlockAsync(cts.Token);

                    if (hashCheckFile)
                    {
                        var checkSum = Convert.ToHexString(hashProvider.Hash.AsSpan());

                        if (!checkSum.Equals(downloadFile.CheckSum!, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadFile.FinishedRangeStreams.Clear();
                            downloadFile.UrlInfo = null;
                            downloadFile.Ranges = null;

                            exceptions.Add(new HashMismatchException(filePath, downloadFile.CheckSum!, checkSum,
                                downloadFile));

                            await RecycleDownloadFile(downloadFile);

                            continue;
                        }

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

                #endregion

                await RecycleDownloadFile(downloadFile);

                downloadFile.RetryCount--;
                downloadFile.OnCompleted(true, null, globalSpeedCalculator.TotalBytes /
                                                     Stopwatch.GetElapsedTime(
                                                         Stopwatch.GetTimestamp() -
                                                         (long)(timeout.TotalSeconds * Stopwatch.Frequency)
                                                     ).TotalSeconds);
                return;
            }
            catch (TaskCanceledException)
            {
                // We don't want to increase the retry count here
                downloadFile.PartialDownloadRetryCount++;
                downloadFile.UrlInfo = null;
                downloadFile.Ranges = null;
            }
            catch (DownloadChunkSplitException ex)
            {
                // Here we are handling the exception thrown by the sub chunk split
                // We don't want to increase the retry count here

                if (ex.Split)
                    subChunkSplitCount++;

                if (ex.InnerException != null)
                    exceptions.Add(ex.InnerException);
            }
            catch (Exception ex)
            {
                downloadFile.PartialDownloadRetryCount++;
                downloadFile.FinishedRangeStreams.Clear();
                downloadFile.UrlInfo = null;
                downloadFile.Ranges = null;
                exceptions.Add(ex);

                var delay = Math.Min(1000 * Math.Pow(2, downloadFile.RetryCount - 1), 10000);
                await Task.Delay((int)delay, CancellationToken.None);
            }
        }

        // We failed to download file
        await RecycleDownloadFile(downloadFile);

        // We need to deduct 1 from the retry count because the last retry will not be counted
        downloadFile.RetryCount--;
        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }

    record PreChunkInfo(
        HttpClient Client,
        int CurrentChunkSplitCount,
        string DownloadUrl,
        DownloadRange Range,
        CancellationTokenSource Cts);

    record ChunkInfo(
        int CurrentChunkSplitCount,
        HttpResponseMessage Response,
        DownloadRange Range,
        CancellationTokenSource Cts);
}