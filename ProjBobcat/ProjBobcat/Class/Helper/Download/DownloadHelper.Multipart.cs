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
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Exceptions;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    private const int DefaultPartialDownloadTimeoutMs = 3000;

    private static HttpClient Head => HttpClientHelper.HeadClient;
    private static HttpClient MultiPart => HttpClientHelper.MultiPartClient;

    record PreChunkInfo(
        BufferBlock<PreChunkInfo> DownloadQueueBuffer,
        FileStream? TempFileStream,
        DownloadRange Range,
        CancellationTokenSource Cts);

    record ChunkInfo(
        BufferBlock<PreChunkInfo> DownloadQueueBuffer,
        FileStream? TempFileStream,
        HttpResponseMessage Response,
        DownloadRange Range,
        CancellationTokenSource Cts);

    /// <summary>
    ///     Receive data from remote stream (only for partial download)
    /// </summary>
    /// <returns>Elapsed time in seconds</returns>
    private static async Task<double> ReceiveFromRemoteStreamAsync(
        Stream remoteStream,
        Stream destStream,
        CancellationToken ct)
    {
        var startTime = Stopwatch.GetTimestamp();

        using var buffer = MemoryPool<byte>.Shared.Rent(DefaultCopyBufferSize);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var bytesRead = await remoteStream.ReadAsync(buffer.Memory, ct);

            if (bytesRead == 0) break;

            await destStream.WriteAsync(buffer.Memory[..bytesRead], ct);
        }

        var duration = Stopwatch.GetElapsedTime(startTime);
        var elapsedTime = duration.TotalSeconds < 0.0001 ? 1 : duration.TotalSeconds;
        
        return elapsedTime;
    }

    private static async Task<(long FileLength, bool CanPartialDownload)?> CanUsePartialDownload(
        string url,
        DownloadSettings downloadSettings,
        CancellationToken ct)
    {
        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);

            if (downloadSettings.Authentication != null)
                headReq.Headers.Authorization = downloadSettings.Authentication;
            if (!string.IsNullOrEmpty(downloadSettings.Host))
                headReq.Headers.Host = downloadSettings.Host;

            using var headRes = await Head.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);

            headRes.EnsureSuccessStatusCode();

            var responseLength = headRes.Content.Headers.ContentLength ?? 0;
            var hasAcceptRanges = headRes.Headers.AcceptRanges.Count != 0;

            using var rangeGetMessage = new HttpRequestMessage(HttpMethod.Get, url);
            rangeGetMessage.Headers.Range = new RangeHeaderValue(0, 0);

            if (downloadSettings.Authentication != null)
                rangeGetMessage.Headers.Authorization = downloadSettings.Authentication;
            if (!string.IsNullOrEmpty(downloadSettings.Host))
                rangeGetMessage.Headers.Host = downloadSettings.Host;

            using var rangeGetRes = await Head.SendAsync(rangeGetMessage, HttpCompletionOption.ResponseHeadersRead, ct);

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
                TempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
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
        DownloadSettings? downloadSettings = null)
    {
        if (downloadFile == null) return;

        downloadSettings ??= DownloadSettings.Default;

        if (downloadSettings.DownloadParts <= 0)
            downloadSettings.DownloadParts = Environment.ProcessorCount;

        var exceptions = new List<Exception>();
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var timeout = downloadSettings.Timeout;
        var trials = downloadSettings.RetryCount == 0 ? 1 : downloadSettings.RetryCount;

        for (var r = 0; r < trials; r++)
        {
            using var cts = new CancellationTokenSource(timeout * Math.Max(1, (r + 1) * 0.5));

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
                            downloadFile.PartialDownloadRetryCount > downloadSettings.RetryCount / 2)
                        {
                            // Reset the retry count
                            downloadFile.RetryCount = 0;

                            await DownloadData(downloadFile, downloadSettings);
                            return;
                        }

                        // Otherwise, we will retry the partial download
                        downloadFile.RetryCount++;
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
                        downloadFile.Ranges.TryAdd(range, range);
                }

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);

                #region Parallel download

                var requestCreationBlock =
                    new TransformBlock<PreChunkInfo, ChunkInfo?>(
                        async preChunkInfo =>
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.GetDownloadUrl());

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            request.Headers.Range = new RangeHeaderValue(preChunkInfo.Range.Start, preChunkInfo.Range.End);

                            try
                            {
                                var downloadTask = await MultiPart.SendAsync(
                                    request,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    preChunkInfo.Cts.Token);

                                return new ChunkInfo(
                                    preChunkInfo.DownloadQueueBuffer,
                                    preChunkInfo.TempFileStream,
                                    downloadTask,
                                    preChunkInfo.Range,
                                    preChunkInfo.Cts);
                            }
                            catch (HttpRequestException e)
                            {
                                Console.WriteLine(e);
                                await preChunkInfo.Cts.CancelAsync();
                            }

                            return null;
                        }, new ExecutionDataflowBlockOptions
                        {
                            BoundedCapacity = downloadSettings.DownloadParts,
                            EnsureOrdered = false,
                            MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                            CancellationToken = cts.Token
                        });

                var aggregatedSpeed = 0U;
                var aggregatedSpeedCount = 0;
                var bytesReceived = 0L;

                var downloadActionBlock = new ActionBlock<ChunkInfo?>(async chunkInfo =>
                {
                    if (chunkInfo == null) return;

                    using var chunkCts = new CancellationTokenSource(DefaultPartialDownloadTimeoutMs);
                    using var res = chunkInfo.Response;

                    // We'll store the written file to a temp file stream, and keep its ref for the future use.
                    var fileToWriteTo = chunkInfo.TempFileStream ?? File.Create(chunkInfo.Range.TempFileName);
                    var currentPos = fileToWriteTo.Position;
                    var elapsedTime = 0d;

                    try
                    {
                        await using var stream = await res.Content.ReadAsStreamAsync(chunkInfo.Cts.Token);

                        // Here we are using chunkCts instead of the main cts
                        elapsedTime = await ReceiveFromRemoteStreamAsync(
                            stream,
                            fileToWriteTo,
                            chunkCts.Token);

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
                    catch (TaskCanceledException)
                    {
                        if (chunkInfo.Range.End - chunkInfo.Range.Start - fileToWriteTo.Position < 1024)
                        {
                            // File is too small to be split, just retry
                            throw;
                        }

                        // If we end up to here, which means either the chunk is too large or the download speed is too slow
                        // So we need to further split the chunk into smaller parts
                        var bytesReceived = fileToWriteTo.Position - currentPos;
                        var chunkLength = chunkInfo.Range.End - chunkInfo.Range.Start + 1;
                        var remaining = chunkLength - bytesReceived;
                        var offset = chunkInfo.Range.Start + fileToWriteTo.Position - 1;
                        var subChunkRanges = CalculateDownloadRanges(remaining, offset, downloadSettings);
                        
                        downloadFile.Ranges.TryRemove(chunkInfo.Range, out _);

                        // We add the sub chunks to the download queue
                        foreach (var range in subChunkRanges)
                        {
                            // Update the original range list
                            downloadFile.Ranges.TryAdd(range, range);
                        }

                        throw;
                    }
                    catch (Exception)
                    {
                        downloadFile.Ranges.TryRemove(chunkInfo.Range, out _);

                        var updatedRange = chunkInfo.Range with { Start = chunkInfo.Range.Start + fileToWriteTo.Position - 1};

                        downloadFile.Ranges.TryAdd(updatedRange, updatedRange);

                        throw;
                    }

                    var addedAggregatedSpeedCount = Interlocked.Increment(ref aggregatedSpeedCount);

                    // Because the feature of HTTP range response,
                    // the first byte of the first range is the last byte of the file.
                    // So we need to skip the first byte of the first range.
                    // (Expect the first part)
                    var correctedSpeed = addedAggregatedSpeedCount > 1 ? fileToWriteTo.Length - 1 : fileToWriteTo.Length;

                    var speed = correctedSpeed / elapsedTime;
                    var addedAggregatedSpeed = Interlocked.Add(ref aggregatedSpeed, (uint)speed);
                    var addedBytesReceived = Interlocked.Add(ref bytesReceived, correctedSpeed);

                    if (downloadSettings.ShowDownloadProgress)
                        downloadFile.OnChanged(
                            (double)addedAggregatedSpeed / addedAggregatedSpeedCount,
                            ProgressValue.Create(addedBytesReceived, downloadFile.UrlInfo.FileLength),
                            addedBytesReceived,
                            downloadFile.UrlInfo.FileLength);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = downloadSettings.DownloadParts,
                    EnsureOrdered = false,
                    MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                var bufferBlock = new BufferBlock<PreChunkInfo>(new DataflowBlockOptions { EnsureOrdered = false });

                bufferBlock.LinkTo(requestCreationBlock, linkOptions);
                requestCreationBlock.LinkTo(downloadActionBlock, linkOptions);

                foreach (var range in downloadFile.GetUndoneRanges())
                    await bufferBlock.SendAsync(new PreChunkInfo(bufferBlock, null, range, cts), cts.Token);

                bufferBlock.Complete();
                await downloadActionBlock.Completion;
                
                var aSpeed = (double)aggregatedSpeed / aggregatedSpeedCount;

                if (!downloadFile.IsDownloadFinished())
                {
                    downloadFile.RetryCount++;
                    continue;
                }

                var hashCheckFile = downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum);

                using var hashProvider = downloadSettings.GetCryptoTransform();

                var fileStream = File.Create(filePath);
                var hashStream = new CryptoStream(fileStream, hashProvider, CryptoStreamMode.Write);

                await using (Stream destStream = hashCheckFile ? hashStream : fileStream)
                {
                    var index = 0;
                    var xxx = downloadFile.Ranges.OrderBy(p => p.Key.Start).Select(p => p.Key).ToList();

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
                }

                if (hashCheckFile)
                {
                    var checkSum = Convert.ToHexString(hashProvider.Hash.AsSpan());

                    if (!checkSum.Equals(downloadFile.CheckSum!, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadFile.RetryCount++;
                        exceptions.Add(new HashMismatchException(filePath, checkSum, downloadFile.CheckSum!));

                        FileHelper.DeleteFileWithRetry(filePath);

                        continue;
                    }
                }

                #endregion

                await RecycleDownloadFile(downloadFile);
                downloadFile.OnCompleted(true, null, aSpeed);
                return;
            }
            catch (Exception ex)
            {
                downloadFile.RetryCount++;
                exceptions.Add(ex);
            }
        }

        // We failed to download file
        await RecycleDownloadFile(downloadFile);

        // We need to deduct 1 from the retry count because the last retry will not be counted
        downloadFile.RetryCount--;
        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }
}