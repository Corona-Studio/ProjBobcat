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
    private static async Task<double> ReceiveFromRemoteStreamAsync(
        Stream remoteStream,
        Stream destStream,
        DownloadFile downloadFile,
        long responseLength,
        CancellationToken ct,
        bool reportDownloadSpeed = false)
    {
        var startTime = Stopwatch.GetTimestamp();

        await remoteStream.CopyToAsync(destStream, ct);
        await destStream.FlushAsync(ct);

        var duration = Stopwatch.GetElapsedTime(startTime);
        var elapsedTime = duration.TotalSeconds == 0 ? 1 : duration.TotalSeconds;
        var speed = responseLength / elapsedTime;

        if (reportDownloadSpeed)
            downloadFile.OnChanged(
                speed,
                1,
                responseLength,
                responseLength);

        return speed;
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

        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();

        for (var i = 0; i <= downloadSettings.RetryCount; i++)
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
                    averageSpeed = await ReceiveFromRemoteStreamAsync(
                        stream,
                        destStream,
                        downloadFile,
                        responseLength,
                        cts.Token,
                        true);

                    if (hashCheckFile && destStream is CryptoStream cStream)
                        await cStream.FlushFinalBlockAsync(cts.Token);
                }

                if (hashCheckFile)
                {
                    var checkSum = Convert.ToHexString(hashProvider.Hash!.AsSpan());

                    if (!(checkSum?.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        downloadFile.RetryCount++;
                        DeleteFileWithRetry(filePath);
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

        downloadFile.OnCompleted(false, new AggregateException(exceptions), 0);
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

    /// <summary>
    ///     File download method (Auto detect download method)
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(
        IEnumerable<DownloadFile> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        var actionBlock = new ActionBlock<DownloadFile>(
            d => AdvancedDownloadFile(d, downloadSettings),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DownloadThread,
                MaxDegreeOfParallelism = DownloadThread,
                EnsureOrdered = false
            });

        foreach (var downloadFile in fileEnumerable) await actionBlock.SendAsync(downloadFile);

        actionBlock.Complete();
        await actionBlock.Completion;
    }

    public static ActionBlock<DownloadFile> AdvancedDownloadListFileActionBlock(DownloadSettings downloadSettings)
    {
        var actionBlock = new ActionBlock<DownloadFile>(
            d => AdvancedDownloadFile(d, downloadSettings),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DownloadThread * 10,
                MaxDegreeOfParallelism = DownloadThread,
                EnsureOrdered = false
            });

        return actionBlock;
    }

    #endregion

    #region Partial download

    private static async Task<(long FileLength, bool CanPartialDownload)> CanUsePartialDownload(
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

    private static void DeleteFileWithRetry(string filePath, int retryCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryCount, 0);

        for (var i = 0; i < retryCount; i++)
            try
            {
                File.Delete(filePath);
                return;
            }
            catch
            {
                // ignored
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

        var isLatestFileCheckSucceeded = true;
        ImmutableList<DownloadRange>? readRanges = null;

        for (var r = 0; r <= downloadSettings.RetryCount; r++)
        {
            using var cts = new CancellationTokenSource(timeout * Math.Max(1, (r + 1) * 0.5));

            try
            {
                #region Get file size

                using var partialDownloadCheckCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var urlInfo = await CanUsePartialDownload(
                    downloadFile.DownloadUri,
                    downloadSettings,
                    partialDownloadCheckCts.Token);

                if (!urlInfo.CanPartialDownload)
                {
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
                var streamBlock =
                    new TransformBlock<DownloadRange, (HttpResponseMessage, DownloadRange)?>(
                        async p =>
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.DownloadUri);

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                            try
                            {
                                var downloadTask = await MultiPart.SendAsync(
                                    request,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    cts.Token);

                                return (downloadTask, p);
                            }
                            catch (HttpRequestException e)
                            {
                                Console.WriteLine(e);
                                await cts.CancelAsync();
                            }

                            return null;
                        }, new ExecutionDataflowBlockOptions
                        {
                            BoundedCapacity = downloadSettings.DownloadParts,
                            EnsureOrdered = false,
                            MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                            CancellationToken = cts.Token
                        });

                var locker = new object();
                var aggregatedSpeed = 0D;
                var aggregatedSpeedCount = 0;

                var writeActionBlock = new ActionBlock<(HttpResponseMessage, DownloadRange)?>(async t =>
                {
                    if (!t.HasValue) return;

                    var pair = t.Value;
                    using var res = pair.Item1;

                    await using (var stream = await res.Content.ReadAsStreamAsync(cts.Token))
                    await using (var fileToWriteTo = File.Create(pair.Item2.TempFileName))
                    {
                        var averageSpeed = await ReceiveFromRemoteStreamAsync(
                            stream,
                            fileToWriteTo,
                            downloadFile,
                            urlInfo.FileLength,
                            cts.Token,
                            downloadSettings.ShowDownloadProgressForPartialDownload);

                        lock (locker)
                        {
                            aggregatedSpeed += averageSpeed;
                            aggregatedSpeedCount++;
                        }
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

                var filesBlock =
                    new TransformManyBlock<IEnumerable<DownloadRange>, DownloadRange>(chunk => chunk,
                        new ExecutionDataflowBlockOptions { EnsureOrdered = false });

                filesBlock.LinkTo(streamBlock, linkOptions);
                streamBlock.LinkTo(writeActionBlock, linkOptions);
                filesBlock.Post(readRanges);

                filesBlock.Complete();

                await writeActionBlock.Completion;

                var aSpeed = aggregatedSpeed / aggregatedSpeedCount;

                if (tasksDone != readRanges.Count)
                {
                    downloadFile.RetryCount++;
                    streamBlock.Complete();
                    writeActionBlock.Complete();
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

                    if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadFile.RetryCount++;
                        isLatestFileCheckSucceeded = false;

                        DeleteFileWithRetry(filePath);

                        continue;
                    }

                    isLatestFileCheckSucceeded = true;
                }

                streamBlock.Complete();
                writeActionBlock.Complete();

                #endregion

                downloadFile.OnCompleted(true, null, aSpeed);
                return;
            }
            catch (Exception ex)
            {
                if (readRanges != null)
                    foreach (var piece in readRanges.Where(piece => File.Exists(piece.TempFileName)))
                        DeleteFileWithRetry(piece.TempFileName);

                downloadFile.RetryCount++;
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            downloadFile.OnCompleted(false, new AggregateException(exceptions), 0);
            return;
        }

        if (!isLatestFileCheckSucceeded)
        {
            downloadFile.OnCompleted(false, null, 0);
            return;
        }

        downloadFile.OnCompleted(true, null, 0);
    }

    #endregion
}