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

    static HttpClient DataClient => HttpClientHelper.DataClient;

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
                using var request = new HttpRequestMessage { RequestUri = new Uri(downloadFile.DownloadUri) };

                if (!string.IsNullOrEmpty(downloadFile.Host))
                    request.Headers.Host = downloadFile.Host;

                using var res =
                    await DataClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                res.EnsureSuccessStatusCode();

                await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);

                using var hash = downloadSettings.GetHashAlgorithm();
                await using var fs = File.Create(filePath);
                await using Stream outputStream =
                    downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum)
                        ? new CryptoStream(fs, hash, CryptoStreamMode.Write)
                        : fs;

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var downloadedBytesCount = 0L;
                var sw = new Stopwatch();

                var tSpeed = 0d;
                var cSpeed = 0;

                using var rentMemory = Pool.Rent(1024);

                while (true)
                {
                    sw.Restart();
                    var bytesRead = await stream.ReadAsync(rentMemory.Memory, cts.Token);
                    sw.Stop();

                    if (bytesRead == 0) break;

                    await outputStream.WriteAsync(rentMemory.Memory[..bytesRead], cts.Token);

                    Interlocked.Add(ref downloadedBytesCount, bytesRead);

                    var elapsedTime = Math.Ceiling(sw.Elapsed.TotalSeconds);
                    var speed = CalculateDownloadSpeed(bytesRead, elapsedTime, SizeUnit.Kb);

                    tSpeed += speed;
                    cSpeed++;

                    downloadFile.OnChanged(
                        speed,
                        (double)downloadedBytesCount / responseLength,
                        downloadedBytesCount,
                        responseLength);
                }

                sw.Stop();

                if (outputStream is CryptoStream cryptoStream)
                    await cryptoStream.FlushFinalBlockAsync(cts.Token);

                if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                {
                    var checkSum = hash.Hash?.BytesToString();

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
                await Task.Delay(250);

                downloadFile.RetryCount++;
                exceptions.Add(e);
                // downloadProperty.OnCompleted(false, e, 0);
            }
        }

        downloadFile.OnCompleted(false, new AggregateException(exceptions), 0);
    }

    #endregion

    public static double CalculateDownloadSpeed(int bytesReceived, double passedSeconds,
        SizeUnit unit = SizeUnit.Mb)
    {
        const double baseNum = 1024;

        return unit switch
        {
            SizeUnit.B => bytesReceived / passedSeconds,
            SizeUnit.Kb => bytesReceived / baseNum / passedSeconds,
            SizeUnit.Mb => bytesReceived / Math.Pow(baseNum, 2) / passedSeconds,
            SizeUnit.Gb => bytesReceived / Math.Pow(baseNum, 3) / passedSeconds,
            SizeUnit.Tb => bytesReceived / Math.Pow(baseNum, 4) / passedSeconds,
            _ => bytesReceived / passedSeconds
        };
    }

    #region 下载一个列表中的文件（自动确定是否使用分片下载）

    /// <summary>
    ///     下载文件方法（自动确定是否使用分片下载）
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadFile(DownloadFile df, DownloadSettings downloadSettings)
    {
        ProcessorHelper.SetMaxThreads();

        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        if (df.FileSize is >= 1048576 or 0)
            await MultiPartDownloadTaskAsync(df, downloadSettings);
        else
            await DownloadData(df, downloadSettings);
    }

    /// <summary>
    ///     下载文件方法（自动确定是否使用分片下载）
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(IEnumerable<DownloadFile> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        ProcessorHelper.SetMaxThreads();

        var filesBlock =
            new TransformManyBlock<IEnumerable<DownloadFile>, DownloadFile>(d =>
            {
                foreach (var df in d.Where(df => !Directory.Exists(df.DownloadPath)))
                    Directory.CreateDirectory(df.DownloadPath);

                return d;
            });

        var actionBlock = new ActionBlock<DownloadFile>(async d =>
        {
            if (d.FileSize is >= 1048576 or 0)
                await MultiPartDownloadTaskAsync(d, downloadSettings);
            else
                await DownloadData(d, downloadSettings);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = DownloadThread,
            MaxDegreeOfParallelism = DownloadThread
        });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        filesBlock.LinkTo(actionBlock, linkOptions);
        filesBlock.Post(fileEnumerable);
        filesBlock.Complete();

        await actionBlock.Completion;
        actionBlock.Complete();
    }

    #endregion

    #region 分片下载

    static HttpClient HeadClient => HttpClientHelper.HeadClient;

    static HttpClient MultiPartClient => HttpClientHelper.MultiPartClient;

    static readonly MemoryPool<byte> Pool = MemoryPool<byte>.Shared;

    /// <summary>
    ///     分片下载方法（异步）
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task MultiPartDownloadTaskAsync(DownloadFile? downloadFile,
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

                using var message = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));

                if (!string.IsNullOrEmpty(downloadFile.Host))
                    message.Headers.Host = downloadFile.Host;

                using var headRes = await HeadClient.SendAsync(message, cts.Token);

                headRes.EnsureSuccessStatusCode();

                var responseLength = headRes.Content.Headers.ContentLength ?? 0;
                var hasAcceptRanges = headRes.Headers.AcceptRanges.Any();

                using var rangeGetMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(downloadFile.DownloadUri));
                rangeGetMessage.Headers.Range = new RangeHeaderValue(0, 0);

                using var rangeGetRes = await HeadClient.SendAsync(rangeGetMessage, cts.Token);

                var parallelDownloadSupported =
                    responseLength != 0 &&
                    hasAcceptRanges &&
                    rangeGetRes.StatusCode == HttpStatusCode.PartialContent &&
                    rangeGetRes.Content.Headers.ContentRange.HasRange &&
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

                readRanges = new List<DownloadRange>();
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
                        TempFileName = Path.GetTempFileName()
                    });
                }

                readRanges = readRanges.OrderBy(r => r.Start).ToList();

                #endregion

                #region Parallel download

                var downloadedBytesCount = 0L;
                var tasksDone = 0;
                var doneRanges = new ConcurrentBag<DownloadRange>();

                var streamBlock =
                    new TransformBlock<DownloadRange, (HttpResponseMessage, DownloadRange)>(
                        async p =>
                        {
                            using var request = new HttpRequestMessage
                                { RequestUri = new Uri(downloadFile.DownloadUri) };
                            if (!string.IsNullOrEmpty(downloadFile.Host))
                                request.Headers.Host = downloadFile.Host;

                            request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                            var downloadTask = await MultiPartClient.SendAsync(
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
                    using var rentMemory = Pool.Rent(1024);

                    var sw = new Stopwatch();

                    while (true)
                    {
                        sw.Restart();
                        var bytesRead = await stream.ReadAsync(rentMemory.Memory, cts.Token);
                        sw.Stop();

                        if (bytesRead == 0)
                            break;

                        await fileToWriteTo.WriteAsync(rentMemory.Memory[..bytesRead], cts.Token);

                        Interlocked.Add(ref downloadedBytesCount, bytesRead);

                        var elapsedTime = Math.Ceiling(sw.Elapsed.TotalSeconds);
                        var speed = CalculateDownloadSpeed(bytesRead, elapsedTime, SizeUnit.Kb);

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
                }

                if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                {
                    var checkSum = (await downloadSettings.HashDataAsync(filePath, cts.Token)).BytesToString();

                    if (!checkSum.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadFile.RetryCount++;
                        isLatestFileCheckSucceeded = false;
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
                // downloadFile.OnCompleted(false, ex, 0);
            }
        }

        if (exceptions.Any())
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