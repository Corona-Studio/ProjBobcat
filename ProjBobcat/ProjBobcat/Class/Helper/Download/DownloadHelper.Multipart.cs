using System;
using System.Buffers;
using System.Collections.Concurrent;
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
    private record UrlInfo(long FileLength);

    // 1 MB
    private const int MinimumChunkSize = 1_000_000; 

    // If the downloaded bytes is greater than 50% of the expected size, we will ignore the chunk cancellation and keep download
    private const double DefaultChunkGiveUpThreshold = 0.85;

    private static bool CanUsePartialDownload(HttpResponseMessage res, long from, long to)
    {
        var parallelDownloadSupported =
            res.Content.Headers.ContentLength == to - from + 1 &&
            res.StatusCode == HttpStatusCode.PartialContent &&
            (res.Content.Headers.ContentRange?.HasRange ?? false);

        return parallelDownloadSupported;
    }

    private static async Task<(long FileLength, bool CanPartialDownload)?> CanUsePartialDownloadAsync(
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

            var parallelDownloadSupported = CanUsePartialDownload(headRes, 0, 0);
            var fullLength = parallelDownloadSupported
                ? headRes.Content.Headers.ContentRange?.Length ?? 0
                : headRes.Content.Headers.ContentLength ?? 0;

            return (fullLength, parallelDownloadSupported);
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
        var remainder = fileLength % downloadSettings.DownloadParts;

        long currentStart = 0;

        for (var i = 0; i < downloadSettings.DownloadParts; i++)
        {
            var currentPartSize = partSize + (i == downloadSettings.DownloadParts - 1 ? remainder : 0);
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

    private static IEnumerable<FileStream> GetFinishedStreamsInorder(ConcurrentDictionary<DownloadRange, FileStream> finishedRanges)
    {
        foreach (var (_, stream) in finishedRanges.OrderBy(p => p.Key.Start))
        {
            yield return stream;
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
        var trials = downloadSettings.RetryCount <= 0 ? 1 : downloadSettings.RetryCount;
        
        // 创建全局速度计算器，用于汇总所有分片下载速度
        var globalSpeedCalculator = new DownloadSpeedCalculator();
        var bytesReceived = 0L;
        var lastProgressUpdateTime = DateTime.UtcNow;
        var progressUpdateInterval = TimeSpan.FromMilliseconds(200); // 限制更新频率
        var finishedRangeStreams = new ConcurrentDictionary<DownloadRange, FileStream>();
        UrlInfo? calculatedUrlInfo = null;

        using var cts = new CancellationTokenSource(downloadSettings.Timeout);

        while (downloadFile.RetryCount < trials)
        {
            // Acquire the download URL
            var downloadUrl = downloadFile.GetDownloadUrl();

            try
            {
                #region Calculate Download Ranges

                if (calculatedUrlInfo == null)
                {
                    #region Get file size

                    using var tempCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    using var partialDownloadCheckCts =
                        CancellationTokenSource.CreateLinkedTokenSource(cts.Token, tempCts.Token);

                    var rawUrlInfo = await CanUsePartialDownloadAsync(
                        downloadUrl,
                        downloadSettings,
                        partialDownloadCheckCts.Token);

                    // If rawUrlInfo == null, means the request is timeout and canceled
                    // If file length is 0 or less than the minimum chunk size, we also fall back to normal download
                    if (rawUrlInfo is not { FileLength: > MinimumChunkSize })
                    {
                        // Reset the retry count
                        downloadFile.RetryCount = 0;

                        await DownloadData(downloadFile, downloadSettings);
                        return;
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

                    calculatedUrlInfo = new UrlInfo(urlInfo.FileLength);
                }

                var calculatedRanges = CalculateDownloadRanges(calculatedUrlInfo.FileLength, 0, downloadSettings)
                    .ToList()
                    .AsReadOnly();

                #endregion

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);

                #region Parallel download

                var requestCreationBlock =
                    new TransformBlock<PreChunkInfo, ChunkInfo?>(
                        async preChunkInfo =>
                        {
                            var from = preChunkInfo.Range.Start;
                            var to = preChunkInfo.Range.End;
                            var range = new RangeHeaderValue(from, to);
                            using var request = new HttpRequestMessage(HttpMethod.Get, preChunkInfo.DownloadUrl);

                            request.Headers.Range = range;

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            var client = preChunkInfo.ClientFactory.CreateClient(DefaultDownloadClientName);
                            var res = await client.SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead,
                                preChunkInfo.MasterCts.Token);

                            if (!res.IsSuccessStatusCode || !CanUsePartialDownload(res, from, to))
                            {
                                // If the response is not successful or the server does not support partial download,
                                // we will not download this chunk and re-enqueue it to the buffer block
                                preChunkInfo.BufferBlock.Post(preChunkInfo);
                                return null;
                            }

                            return new ChunkInfo(
                                preChunkInfo.ClientFactory,
                                preChunkInfo.DownloadUrl,
                                res,
                                preChunkInfo.Range,
                                preChunkInfo.UrlInfo,
                                preChunkInfo.BufferBlock,
                                preChunkInfo.PartialDownloadCts,
                                preChunkInfo.MasterCts);
                        }, new ExecutionDataflowBlockOptions
                        {
                            EnsureOrdered = false,
                            MaxDegreeOfParallelism = downloadSettings.DownloadThread,
                            CancellationToken = cts.Token
                        });

                var downloadActionBlock = new ActionBlock<ChunkInfo?>(async chunkInfo =>
                {
                    if (chunkInfo == null) return;

                    using var res = chunkInfo.Response;
                    using var buffer = MemoryPool<byte>.Shared.Rent(MinimumChunkSize);

                    // We'll store the written file to a temp file stream, and keep its ref for the future use.
                    var fileToWriteTo = File.Create(GetTempFilePath());
                    var receivedBytes = 0L;
                    var speed = 0d;

                    await using var stream = await res.Content.ReadAsStreamAsync(chunkInfo.MasterCts.Token);

                    while (true)
                    {
                        // If PartialDownloadCts is canceled, it means for the batch of sub-chunks, one of the chunk finished downloading
                        if (chunkInfo.PartialDownloadCts.IsCancellationRequested)
                        {
                            var downloadedPercentage = (double)receivedBytes / chunkInfo.Range.Length;

                            if (downloadedPercentage < DefaultChunkGiveUpThreshold)
                            {
                                // If the downloaded bytes is less than the threshold, we will cancel the download
                                // and further split the chunk if possible
                                break;
                            }
                        }

                        var bytesRead = await stream.ReadAsync(buffer.Memory, chunkInfo.MasterCts.Token);

                        if (bytesRead == 0) break;

                        receivedBytes += bytesRead;
                        await fileToWriteTo.WriteAsync(buffer.Memory[..bytesRead], chunkInfo.MasterCts.Token);

                        // 计算速度
                        speed = globalSpeedCalculator.AddSample(bytesRead);
                    }

                    // Check if the actual range is finished
                    if (chunkInfo.Range.Length != receivedBytes)
                    {
                        var undoneRange = chunkInfo.Range with
                        {
                            Start = chunkInfo.Range.Start + receivedBytes
                        };

                        // Enqueue the rest of the range to the buffer block
                        if (undoneRange.Length - receivedBytes < DefaultChunkGiveUpThreshold)
                        {
                            // If the remaining range is too small, we will not split it further
                            // Just post the remaining range to the buffer block
                            ArgumentOutOfRangeException.ThrowIfEqual(
                                chunkInfo.BufferBlock.Post(new PreChunkInfo(
                                    chunkInfo.ClientFactory,
                                    chunkInfo.DownloadUrl,
                                    undoneRange,
                                    chunkInfo.UrlInfo,
                                    chunkInfo.BufferBlock,
                                    new CancellationTokenSource(),
                                    chunkInfo.MasterCts)),
                                false);
                        }
                        else
                        {
                            var splitRanges = CalculateDownloadRanges(undoneRange.Length, undoneRange.Start, downloadSettings)
                                .ToList()
                                .AsReadOnly();

                            foreach (var range in splitRanges)
                            {
                                // Add the split range to the buffer block
                                ArgumentOutOfRangeException.ThrowIfEqual(
                                    chunkInfo.BufferBlock.Post(new PreChunkInfo(
                                        chunkInfo.ClientFactory,
                                        chunkInfo.DownloadUrl,
                                        range,
                                        chunkInfo.UrlInfo,
                                        chunkInfo.BufferBlock,
                                        new CancellationTokenSource(),
                                        chunkInfo.MasterCts)),
                                    false);
                            }
                        }
                    }

                    var finishedRange = chunkInfo.Range with { End = chunkInfo.Range.Start + receivedBytes - 1 };

                    finishedRangeStreams.AddOrUpdate(finishedRange, fileToWriteTo, (_, oldStream) =>
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

                    // 更新总下载字节数
                    var addedBytesReceived = Interlocked.Add(ref bytesReceived, receivedBytes);
                    var actualFileLength = chunkInfo.UrlInfo.FileLength;

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
                            ProgressValue.Create(addedBytesReceived, actualFileLength),
                            addedBytesReceived,
                            actualFileLength);
                    }

                    await chunkInfo.PartialDownloadCts.CancelAsync();

                    if (actualFileLength == addedBytesReceived)
                    {
                        // If the file length is equal to the downloaded bytes, we can complete the download
                        chunkInfo.BufferBlock.Complete();
                        chunkInfo.PartialDownloadCts.Dispose();
                    }
                }, new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = false,
                    MaxDegreeOfParallelism = downloadSettings.DownloadThread,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                var bufferBlock = new BufferBlock<PreChunkInfo>(new DataflowBlockOptions { EnsureOrdered = false });

                bufferBlock.LinkTo(requestCreationBlock, linkOptions);
                requestCreationBlock.LinkTo(downloadActionBlock, linkOptions);

                var partialDownloadCts = new CancellationTokenSource();

                foreach (var range in calculatedRanges)
                {
                    var chunkInfo = new PreChunkInfo(
                        downloadSettings.HttpClientFactory,
                        downloadUrl,
                        range,
                        calculatedUrlInfo,
                        bufferBlock,
                        partialDownloadCts,
                        cts);

                    await bufferBlock.SendAsync(chunkInfo, cts.Token);
                }

                var startTime = Stopwatch.GetTimestamp();

                await downloadActionBlock.Completion;

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
                        finishedRangeStreams.Sum(p => p.Value.Length))
                    : File.Create(filePath);
                var hashStream = new CryptoStream(ms, hashProvider, CryptoStreamMode.Write);

                await using (var destStream = hashCheckFile ? hashStream : ms)
                {
                    foreach (var inputFileStream in GetFinishedStreamsInorder(finishedRangeStreams))
                    {
                        // Reset the stream position
                        inputFileStream.Seek(0, SeekOrigin.Begin);
                        await inputFileStream.CopyToAsync(destStream, cts.Token);
                    }

                    await destStream.FlushAsync(cts.Token);

                    if (hashCheckFile && destStream is CryptoStream cStream)
                        await cStream.FlushFinalBlockAsync(cts.Token);

                    if (hashCheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                    {
                        var checkSum = Convert.ToHexString(hashProvider.Hash.AsSpan());

                        if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                        {
                            finishedRangeStreams.Clear();

                            exceptions.Add(new HashMismatchException(filePath, downloadFile.CheckSum, checkSum));

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

                downloadFile.OnCompleted(
                    true,
                    null,
                    globalSpeedCalculator.TotalBytes / Stopwatch.GetElapsedTime(startTime).TotalSeconds);

                return;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Here we are handling the exception thrown by the size check
                // We don't want to increase the retry count here
                // Just fall back to normal download
                downloadFile.RetryCount = 0;
                downloadFile.OnChanged(
                    0,
                    ProgressValue.Start,
                    0,
                    0);

                await DownloadData(downloadFile, downloadSettings);
                return;
            }
            catch (HttpRequestException e)
            {
                downloadFile.RetryCount++;
                exceptions.Add(e);

                if (e.StatusCode != HttpStatusCode.NotFound)
                {
                    var delay = Math.Min(1000 * Math.Pow(2, downloadFile.RetryCount), 10000);
                    await Task.Delay((int)delay, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                downloadFile.RetryCount++;

                finishedRangeStreams.Clear();
                exceptions.Add(ex);

                downloadFile.OnChanged(
                    0,
                    ProgressValue.Start,
                    0,
                    0);

                var delay = Math.Min(1000 * Math.Pow(2, downloadFile.RetryCount - 1), 10000);
                await Task.Delay((int)delay, CancellationToken.None);
            }
        }

        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }

    record PreChunkInfo(
        IHttpClientFactory ClientFactory,
        string DownloadUrl,
        DownloadRange Range,
        UrlInfo UrlInfo,
        BufferBlock<PreChunkInfo> BufferBlock,
        CancellationTokenSource PartialDownloadCts,
        CancellationTokenSource MasterCts);

    record ChunkInfo(
        IHttpClientFactory ClientFactory,
        string DownloadUrl,
        HttpResponseMessage Response,
        DownloadRange Range,
        UrlInfo UrlInfo,
        BufferBlock<PreChunkInfo> BufferBlock,
        CancellationTokenSource PartialDownloadCts,
        CancellationTokenSource MasterCts);
}