using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

namespace ProjBobcat.Class.Helper;

public static class DownloadHelper
{
    /// <summary>
    ///     Download thread count
    /// </summary>
    public static int DownloadThread { get; set; } = 8;

    private static HttpClient Head => HttpClientHelper.HeadClient;
    private static HttpClient Data => HttpClientHelper.DataClient;
    private static HttpClient MultiPart => HttpClientHelper.MultiPartClient;

    /// <summary>
    ///     Receive data from remote stream
    /// </summary>
    /// <returns>Average download speed</returns>
    private static async Task<(long ResLength, double ElapsedTime)> ReceiveFromRemoteStreamAsync(
        Stream remoteStream,
        Stream destStream,
        long responseLength,
        CancellationToken ct)
    {
        var startTime = Stopwatch.GetTimestamp();

        await remoteStream.CopyToAsync(destStream, ct);
        await destStream.FlushAsync(ct);

        var duration = Stopwatch.GetElapsedTime(startTime);
        var elapsedTime = duration.TotalSeconds == 0 ? 1 : duration.TotalSeconds;
        
        return (responseLength, elapsedTime);
    }

    #region Download data

    /// <summary>
    ///     Simple download data impl
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task DownloadData(DownloadFile downloadFile, DownloadSettings? downloadSettings = null)
    {
        downloadSettings ??= DownloadSettings.Default;

        var trials = downloadSettings.RetryCount == 0 ? 1 : downloadSettings.RetryCount;
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();

        for (var i = 0; i < trials; i++)
        {
            using var cts = new CancellationTokenSource(downloadSettings.Timeout * Math.Max(1, i + 1));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.DownloadUri);

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

                double averageSpeed;

                var outputStream = File.Create(filePath);
                var cryptoStream = new CryptoStream(outputStream, hashProvider, CryptoStreamMode.Write, true);

                await using (var stream = await res.Content.ReadAsStreamAsync(cts.Token))
                await using (Stream destStream = hashCheckFile ? cryptoStream : outputStream)
                {
                    var stats = await ReceiveFromRemoteStreamAsync(
                        stream,
                        destStream,
                        responseLength,
                        cts.Token);

                    averageSpeed = stats.ResLength / stats.ElapsedTime;

                    downloadFile.OnChanged(
                        averageSpeed,
                        1,
                        responseLength,
                        responseLength);

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

        // We need to deduct 1 from the retry count because the last retry will not be counted
        downloadFile.RetryCount--;
        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }

    #endregion

    public static string AutoFormatSpeedString(double speedInBytePerSecond)
    {
        var speed = AutoFormatSpeed(speedInBytePerSecond);
        var unit = speed.Unit switch
        {
            SizeUnit.B => "B / s",
            SizeUnit.Kb => "Kb / s",
            SizeUnit.Mb => "Mb / s",
            SizeUnit.Gb => "Gb / s",
            SizeUnit.Tb => "Tb / s",
            _ => "B / s"
        };

        return $"{speed.Speed:F} {unit}";
    }

    public static (double Speed, SizeUnit Unit) AutoFormatSpeed(double transferSpeed)
    {
        const double baseNum = 1024;
        const double mbNum = baseNum * baseNum;
        const double gbNum = baseNum * mbNum;
        const double tbNum = baseNum * gbNum;

        // Auto choose the unit
        var unit = transferSpeed switch
        {
            >= tbNum => SizeUnit.Tb,
            >= gbNum => SizeUnit.Gb,
            >= mbNum => SizeUnit.Mb,
            >= baseNum => SizeUnit.Kb,
            _ => SizeUnit.B
        };

        var convertedSpeed = unit switch
        {
            SizeUnit.Kb => transferSpeed / baseNum,
            SizeUnit.Mb => transferSpeed / mbNum,
            SizeUnit.Gb => transferSpeed / gbNum,
            SizeUnit.Tb => transferSpeed / tbNum,
            _ => transferSpeed
        };

        return (convertedSpeed, unit);
    }

    #region Download a list of files

    /// <summary>
    ///     Advanced file download impl
    /// </summary>
    /// <param name="df"></param>
    /// <param name="downloadSettings"></param>
    private static Task AdvancedDownloadFile(DownloadFile df, DownloadSettings downloadSettings)
    {
        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        return df.FileSize is >= 1048576 or 0
            ? MultiPartDownloadTaskAsync(df, downloadSettings)
            : DownloadData(df, downloadSettings);
    }

    private static (BufferBlock<DownloadFile> Input, ActionBlock<DownloadFile> Execution) BuildAdvancedDownloadTplBlock(DownloadSettings downloadSettings)
    {
        var bufferBlock = new BufferBlock<DownloadFile>(new DataflowBlockOptions { EnsureOrdered = false });
        var actionBlock = new ActionBlock<DownloadFile>(
            d => AdvancedDownloadFile(d, downloadSettings),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DownloadThread * 10,
                MaxDegreeOfParallelism = DownloadThread,
                EnsureOrdered = false
            });

        bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });
        
        return (bufferBlock, actionBlock);
    }

    /// <summary>
    ///     File download method (Auto detect download method)
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(
        IEnumerable<DownloadFile> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        var blocks = BuildAdvancedDownloadTplBlock(downloadSettings);

        foreach (var downloadFile in fileEnumerable)
            await blocks.Input.SendAsync(downloadFile);

        blocks.Input.Complete();
        await blocks.Execution.Completion;
    }

    public static (BufferBlock<DownloadFile> Input, ActionBlock<DownloadFile> Execution)
        AdvancedDownloadListFileActionBlock(DownloadSettings downloadSettings) =>
        BuildAdvancedDownloadTplBlock(downloadSettings);

    #endregion

    #region Partial download

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
                Start = from,
                End = to,
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
        DownloadFile? downloadFile,
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

        ImmutableList<DownloadRange>? readRanges = null;

        for (var r = 0; r < trials; r++)
        {
            using var cts = new CancellationTokenSource(timeout * Math.Max(1, (r + 1) * 0.5));

            try
            {
                #region Get file size

                using var partialDownloadCheckCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var rawUrlInfo = await CanUsePartialDownload(
                    downloadFile.DownloadUri,
                    downloadSettings,
                    partialDownloadCheckCts.Token);

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

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);

                #region Calculate ranges

                readRanges = CalculateDownloadRanges(urlInfo.FileLength, downloadSettings).Reverse().ToImmutableList();

                #endregion

                #region Parallel download

                var tasksDone = 0;
                var requestCreationBlock =
                    new TransformBlock<(DownloadRange, CancellationTokenSource), (HttpResponseMessage, DownloadRange, CancellationTokenSource)?>(
                        async pair =>
                        {
                            var (range, pCts) = pair;

                            using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.DownloadUri);

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            request.Headers.Range = new RangeHeaderValue(range.Start, range.End);

                            try
                            {
                                var downloadTask = await MultiPart.SendAsync(
                                    request,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    pCts.Token);

                                return (downloadTask, range, pCts);
                            }
                            catch (HttpRequestException e)
                            {
                                Console.WriteLine(e);
                                await pCts.CancelAsync();
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

                var downloadActionBlock = new ActionBlock<(HttpResponseMessage, DownloadRange, CancellationTokenSource)?>(async pair =>
                {
                    if (!pair.HasValue) return;

                    var (response, range, pCts) = pair.Value;
                    using var res = response;

                    await using (var stream = await res.Content.ReadAsStreamAsync(pCts.Token))
                    await using (var fileToWriteTo = File.Create(range.TempFileName))
                    {
                        var stats = await ReceiveFromRemoteStreamAsync(
                            stream,
                            fileToWriteTo,
                            urlInfo.FileLength,
                            pCts.Token);

                        var speed = stats.ResLength / stats.ElapsedTime;
                        var addedAggregatedSpeed = Interlocked.Add(ref aggregatedSpeed, (uint)speed);
                        var addedAggregatedSpeedCount = Interlocked.Increment(ref aggregatedSpeedCount);
                        var addedBytesReceived = Interlocked.Add(ref bytesReceived, stats.ResLength);

                        if (downloadSettings.ShowDownloadProgressForPartialDownload)
                            downloadFile.OnChanged(
                                (double)addedAggregatedSpeed / addedAggregatedSpeedCount,
                                (double)addedBytesReceived / urlInfo.FileLength,
                                addedBytesReceived,
                                urlInfo.FileLength);
                    }

                    Interlocked.Add(ref tasksDone, 1);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = downloadSettings.DownloadParts,
                    EnsureOrdered = false,
                    MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                var bufferBlock = new BufferBlock<(DownloadRange, CancellationTokenSource)>(new DataflowBlockOptions { EnsureOrdered = false });

                bufferBlock.LinkTo(requestCreationBlock, linkOptions);
                requestCreationBlock.LinkTo(downloadActionBlock, linkOptions);

                foreach (var range in readRanges)
                    await bufferBlock.SendAsync((range, cts), cts.Token);

                bufferBlock.Complete();

                await downloadActionBlock.Completion;

                var aSpeed = (double)aggregatedSpeed / aggregatedSpeedCount;

                if (tasksDone != readRanges.Count)
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

                    foreach (var inputFilePath in readRanges)
                    {
                        await using var inputStream = File.OpenRead(inputFilePath.TempFileName);

                        // Because the feature of HTTP range response,
                        // the first byte of the first range is the last byte of the file.
                        // So we need to skip the first byte of the first range.
                        // (Expect the first part)
                        if (index != 0)
                            inputStream.Seek(1, SeekOrigin.Begin);

                        await inputStream.CopyToAsync(destStream, cts.Token);

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

                downloadFile.OnCompleted(true, null, aSpeed);
                return;
            }
            catch (Exception ex)
            {
                if (readRanges != null)
                    foreach (var piece in readRanges.Where(piece => File.Exists(piece.TempFileName)))
                        FileHelper.DeleteFileWithRetry(piece.TempFileName);

                downloadFile.RetryCount++;
                exceptions.Add(ex);
            }
        }

        // We need to deduct 1 from the retry count because the last retry will not be counted
        downloadFile.RetryCount--;
        downloadFile.OnCompleted(false, new AggregateException(exceptions), -1);
    }

    #endregion
}