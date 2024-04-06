using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     下载帮助器。
/// </summary>
public static class DownloadHelper
{
    /// <summary>
    ///     下载线程
    /// </summary>
    public static int DownloadThread { get; set; } = 8;

    static HttpClient Head => HttpClientHelper.HeadClient;
    static HttpClient Data => HttpClientHelper.DataClient;
    static HttpClient MultiPart => HttpClientHelper.MultiPartClient;

    #region 下载数据

    /// <summary>
    ///     下载文件（通过线程池）
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

                await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
                await using var outputStream = File.Create(filePath);

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var downloadedBytesCount = 0L;
                var sw = new Stopwatch();

                var tSpeed = 0d;
                var cSpeed = 0;
                var lastWrotePos = 0L;

                while (true)
                {
                    sw.Restart();
                    await stream.CopyToAsync(outputStream, cts.Token);
                    var bytesRead = outputStream.Position - lastWrotePos;
                    lastWrotePos = outputStream.Position;
                    sw.Stop();

                    if (bytesRead == 0) break;

                    downloadedBytesCount += bytesRead;

                    var elapsedTime = sw.Elapsed.TotalSeconds == 0 ? 1 : sw.Elapsed.TotalSeconds;
                    var speed = bytesRead / elapsedTime;

                    tSpeed += speed;
                    cSpeed++;

                    downloadFile.OnChanged(
                        speed,
                        (double)downloadedBytesCount / responseLength,
                        downloadedBytesCount,
                        responseLength);
                }

                sw.Stop();

                if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                {
                    await outputStream.FlushAsync(cts.Token);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    var checkSum = (await downloadSettings.HashDataAsync(outputStream, cts.Token)).BytesToString();

                    if (!(checkSum?.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        downloadFile.RetryCount++;
                        continue;
                    }
                }

                var aSpeed = tSpeed / cSpeed;
                downloadFile.OnCompleted(true, null, aSpeed);

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

    #region 下载一个列表中的文件（自动确定是否使用分片下载）

    /// <summary>
    ///     下载文件方法（自动确定是否使用分片下载）
    /// </summary>
    /// <param name="df"></param>
    /// <param name="downloadSettings"></param>
    public static Task AdvancedDownloadFile(DownloadFile df, DownloadSettings downloadSettings)
    {
        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        if (df.FileSize is >= 1048576 or 0)
            return MultiPartDownloadTaskAsync(df, downloadSettings);

        return DownloadData(df, downloadSettings);
    }

    /// <summary>
    ///     下载文件方法（自动确定是否使用分片下载）
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(
        IEnumerable<DownloadFile> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        ProcessorHelper.SetMaxThreads();

        var actionBlock = new ActionBlock<DownloadFile>(
            d => AdvancedDownloadFile(d, downloadSettings),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DownloadThread * 2,
                MaxDegreeOfParallelism = DownloadThread
            });

        foreach (var downloadFile in fileEnumerable)
        {
            await actionBlock.SendAsync(downloadFile);
        }

        actionBlock.Complete();
        await actionBlock.Completion;
    }

    #endregion

    #region 分片下载

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
        var timeout = TimeSpan.FromMilliseconds(downloadSettings.Timeout * 2);

        var isLatestFileCheckSucceeded = true;
        List<DownloadRange>? readRanges = null;

        for (var r = 0; r <= downloadSettings.RetryCount; r++)
        {
            using var cts = new CancellationTokenSource(timeout * Math.Max(1, r + 1));

            try
            {
                #region Get file size

                using var headReq = new HttpRequestMessage(HttpMethod.Head, downloadFile.DownloadUri);

                if (downloadSettings.Authentication != null)
                    headReq.Headers.Authorization = downloadSettings.Authentication;
                if (!string.IsNullOrEmpty(downloadSettings.Host))
                    headReq.Headers.Host = downloadSettings.Host;

                using var headRes = await Head.SendAsync(headReq, cts.Token);

                headRes.EnsureSuccessStatusCode();

                var responseLength = headRes.Content.Headers.ContentLength ?? 0;
                var hasAcceptRanges = headRes.Headers.AcceptRanges.Count != 0;

                using var rangeGetMessage = new HttpRequestMessage(HttpMethod.Get, downloadFile.DownloadUri);
                rangeGetMessage.Headers.Range = new RangeHeaderValue(0, 0);

                if (downloadSettings.Authentication != null)
                    rangeGetMessage.Headers.Authorization = downloadSettings.Authentication;
                if (!string.IsNullOrEmpty(downloadSettings.Host))
                    rangeGetMessage.Headers.Host = downloadSettings.Host;

                using var rangeGetRes = await Head.SendAsync(rangeGetMessage, cts.Token);

                var parallelDownloadSupported =
                    responseLength != 0 &&
                    hasAcceptRanges &&
                    rangeGetRes.StatusCode == HttpStatusCode.PartialContent &&
                    (rangeGetRes.Content.Headers.ContentRange?.HasRange ?? false) &&
                    rangeGetRes.Content.Headers.ContentLength == 1;

                if (!parallelDownloadSupported)
                {
                    await DownloadData(downloadFile, downloadSettings);
                    return;
                }

                #endregion

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);

                #region Calculate ranges

                readRanges = [];
                var partSize = responseLength / downloadSettings.DownloadParts;
                var totalSize = responseLength;

                while (totalSize > 0)
                {
                    //计算分片
                    var to = totalSize;
                    var from = totalSize - partSize;

                    if (from < 0) from = 0;

                    totalSize -= partSize;

                    readRanges.Add(new DownloadRange
                    {
                        Start = from,
                        End = to,
                        TempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
                    });
                }

                #endregion

                #region Parallel download

                var downloadedBytesCount = 0L;
                var tasksDone = 0;
                var doneRanges = new ConcurrentBag<DownloadRange>();

                var streamBlock =
                    new TransformBlock<DownloadRange, (HttpResponseMessage, DownloadRange)>(
                        async p =>
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, downloadFile.DownloadUri);

                            if (downloadSettings.Authentication != null)
                                request.Headers.Authorization = downloadSettings.Authentication;
                            if (!string.IsNullOrEmpty(downloadSettings.Host))
                                request.Headers.Host = downloadSettings.Host;

                            request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                            var downloadTask = await MultiPart.SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead,
                                cts.Token);

                            return (downloadTask, p);
                        }, new ExecutionDataflowBlockOptions
                        {
                            BoundedCapacity = downloadSettings.DownloadParts,
                            MaxDegreeOfParallelism = downloadSettings.DownloadParts
                        });

                var tSpeed = 0D;
                var cSpeed = 0;

                var writeActionBlock = new ActionBlock<(HttpResponseMessage, DownloadRange)>(async t =>
                {
                    using var res = t.Item1;

                    await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
                    await using var fileToWriteTo = File.Create(t.Item2.TempFileName);
                    
                    var sw = new Stopwatch();
                    var lastWrotePos = 0L;

                    while (true)
                    {
                        sw.Restart();

                        await stream.CopyToAsync(fileToWriteTo, cts.Token);
                        var bytesRead = fileToWriteTo.Position - lastWrotePos;
                        lastWrotePos = fileToWriteTo.Position;

                        sw.Stop();

                        if (bytesRead == 0)
                            break;

                        Interlocked.Add(ref downloadedBytesCount, bytesRead);

                        var elapsedTime = Math.Ceiling(sw.Elapsed.TotalSeconds);
                        var speed = bytesRead / elapsedTime;

                        tSpeed += speed;
                        cSpeed++;

                        downloadFile.OnChanged(
                            speed,
                            (double)downloadedBytesCount / responseLength,
                            downloadedBytesCount,
                            responseLength);
                    }

                    sw.Stop();

                    Interlocked.Add(ref tasksDone, 1);
                    doneRanges.Add(t.Item2);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = downloadSettings.DownloadParts,
                    MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                var filesBlock =
                    new TransformManyBlock<IEnumerable<DownloadRange>, DownloadRange>(chunk => chunk,
                        new ExecutionDataflowBlockOptions());

                filesBlock.LinkTo(streamBlock, linkOptions);
                streamBlock.LinkTo(writeActionBlock, linkOptions);
                filesBlock.Post(readRanges);

                filesBlock.Complete();

                await writeActionBlock.Completion;

                var aSpeed = tSpeed / cSpeed;

                if (doneRanges.Count != readRanges.Count)
                {
                    downloadFile.RetryCount++;
                    streamBlock.Complete();
                    writeActionBlock.Complete();
                    continue;
                }

                await using (var outputStream = File.Create(filePath))
                {
                    foreach (var inputFilePath in readRanges)
                    {
                        await using var inputStream = File.OpenRead(inputFilePath.TempFileName);
                        outputStream.Seek(inputFilePath.Start, SeekOrigin.Begin);

                        await inputStream.CopyToAsync(outputStream, cts.Token);
                    }

                    await outputStream.FlushAsync(cts.Token);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                    {
                        var checkSum = (await downloadSettings.HashDataAsync(outputStream, cts.Token)).BytesToString();

                        if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadFile.RetryCount++;
                            isLatestFileCheckSucceeded = false;
                            continue;
                        }

                        isLatestFileCheckSucceeded = true;
                    }
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
                        try
                        {
                            File.Delete(piece.TempFileName);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }

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