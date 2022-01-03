using System;
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
    const int BufferSize = 1024;

    /// <summary>
    ///     下载线程
    /// </summary>
    public static int DownloadThread { get; set; }

    static HttpClient DataClient => HttpClientHelper.GetNewClient(HttpClientHelper.DataClientName);

    static async Task<bool> CheckFile(DownloadFile df)
    {
#pragma warning disable CA5350 // 不要使用弱加密算法
        using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

        try
        {
            var hash = await CryptoHelper.ComputeFileHashAsync(df.DownloadPath, hA);

            return string.IsNullOrEmpty(df.CheckSum) || hash.Equals(df.CheckSum, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    #region 下载一个列表中的文件（自动确定是否使用分片下载）

    /// <summary>
    ///     下载文件方法（自动确定是否使用分片下载）
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadParts"></param>
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

        var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};
        filesBlock.LinkTo(actionBlock, linkOptions);
        filesBlock.Post(fileEnumerable);
        filesBlock.Complete();

        await actionBlock.Completion;
        actionBlock.Complete();
    }

    #endregion

    #region 下载数据

    /// <summary>
    ///     下载文件（通过线程池）
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="downloadSettings"></param>
    /// <returns></returns>
    public static async Task DownloadData(DownloadFile downloadFile, DownloadSettings downloadSettings)
    {
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var exceptions = new List<Exception>();

        for (var i = 0; i <= downloadSettings.RetryCount; i++)
        {
            using var cts = new CancellationTokenSource(downloadSettings.Timeout);

            try
            {
                using var request = new HttpRequestMessage {RequestUri = new Uri(downloadFile.DownloadUri)};

                if (!string.IsNullOrEmpty(downloadFile.Host))
                    request.Headers.Host = downloadFile.Host;

                using var res =
                    await DataClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                res.EnsureSuccessStatusCode();

                await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
                await using var fileToWriteTo =
                    File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                var responseLength = res.Content.Headers.ContentLength ?? 0;
                var downloadedBytesCount = 0L;
                var buffer = new byte[BufferSize];
                var sw = new Stopwatch();

                var tSpeed = 0d;
                var cSpeed = 0;

                using var hash = downloadSettings.GetHashAlgorithm();

                while (true)
                {
                    sw.Restart();
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cts.Token);
                    sw.Stop();

                    if (bytesRead == 0) break;

                    await fileToWriteTo.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);

                    if (downloadSettings.CheckFile)
                        hash.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                    Interlocked.Add(ref downloadedBytesCount, bytesRead);

                    var elapsedTime = Math.Ceiling(sw.Elapsed.TotalSeconds);
                    var speed = CalculateDownloadSpeed(bytesRead, elapsedTime, SizeUnit.Kb);

                    tSpeed += speed;
                    cSpeed++;

                    downloadFile.OnChanged(
                        speed,
                        (double) downloadedBytesCount / responseLength,
                        downloadedBytesCount,
                        responseLength);
                }

                hash.TransformFinalBlock(buffer, 0, 0);
                sw.Stop();

                if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                {
                    var checkResult = CryptoHelper.ToString(hash.Hash)
                        .Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase);
                    if (!checkResult)
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
                await Task.Delay(500);

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

    #region 分片下载

    static HttpClient HeadClient => HttpClientHelper.GetNewClient(HttpClientHelper.HeadClientName);

    static HttpClient MultiPartClient =>
        HttpClientHelper.GetNewClient(HttpClientHelper.MultiPartClientName);

    /// <summary>
    ///     分片下载方法（异步）
    /// </summary>
    /// <param name="downloadFile"></param>
    /// <param name="retryCount"></param>
    /// <param name="checkFile"></param>
    /// <param name="numberOfParts"></param>
    /// <returns></returns>
    public static async Task MultiPartDownloadTaskAsync(DownloadFile downloadFile, DownloadSettings downloadSettings)
    {
        if (downloadFile == null) return;

        if (downloadSettings.DownloadParts <= 0)
            downloadSettings.DownloadParts = Environment.ProcessorCount;

        var exceptions = new List<Exception>();
        var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var timeout = TimeSpan.FromMilliseconds(downloadSettings.Timeout * 2);

        List<DownloadRange> readRanges = null;

        for (var r = 0; r <= downloadSettings.RetryCount; r++)
        {
            using var cts = new CancellationTokenSource(timeout);

            try
            {
                #region Get file size

                using var message = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));

                if (!string.IsNullOrEmpty(downloadFile.Host))
                    message.Headers.Host = downloadFile.Host;

                using var headRes = await HeadClient.SendAsync(message, cts.Token);

                headRes.EnsureSuccessStatusCode();

                var hasAcceptRanges = headRes.Headers.AcceptRanges.Any();
                var hasRightStatusCode = headRes.StatusCode == HttpStatusCode.PartialContent;
                var responseLength = headRes.Content.Headers.ContentRange?.Length ?? 0;
                var contentLength = headRes.Content.Headers.ContentLength ?? 0;
                var parallelDownloadSupported =
                    hasAcceptRanges &&
                    hasRightStatusCode &&
                    contentLength == 2 &&
                    responseLength != 0;

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
                var partSize = (long) Math.Round((double) responseLength / downloadSettings.DownloadParts);
                var previous = 0L;

                if (responseLength > downloadSettings.DownloadParts)
                    for (var i = partSize; i <= responseLength; i += partSize)
                        if (i + partSize < responseLength)
                        {
                            var start = previous;

                            readRanges.Add(new DownloadRange
                            {
                                Start = start,
                                End = i,
                                TempFileName = Path.GetTempFileName()
                            });

                            previous = i;
                        }
                        else
                        {
                            var start = previous;

                            readRanges.Add(new DownloadRange
                            {
                                Start = start,
                                End = responseLength,
                                TempFileName = Path.GetTempFileName()
                            });

                            previous = i;
                        }
                else
                    readRanges.Add(new DownloadRange
                    {
                        End = responseLength,
                        Start = 0,
                        TempFileName = Path.GetTempFileName()
                    });

                #endregion

                #region Parallel download

                var downloadedBytesCount = 0L;
                var tasksDone = 0;
                var doneRanges = new ConcurrentBag<DownloadRange>();

                var streamBlock =
                    new TransformBlock<DownloadRange, ValueTuple<Task<HttpResponseMessage>, DownloadRange>>(
                        p =>
                        {
                            using var request = new HttpRequestMessage {RequestUri = new Uri(downloadFile.DownloadUri)};
                            if (!string.IsNullOrEmpty(downloadFile.Host))
                                request.Headers.Host = downloadFile.Host;

                            request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                            var downloadTask = MultiPartClient.SendAsync(request,
                                HttpCompletionOption.ResponseHeadersRead,
                                CancellationToken.None);

                            doneRanges.Add(p);

                            return (downloadTask, p);
                        }, new ExecutionDataflowBlockOptions
                        {
                            BoundedCapacity = downloadSettings.DownloadParts,
                            MaxDegreeOfParallelism = downloadSettings.DownloadParts
                        });

                var tSpeed = 0D;
                var cSpeed = 0;

                var writeActionBlock = new ActionBlock<(Task<HttpResponseMessage>, DownloadRange)>(async t =>
                {
                    using var res = await t.Item1;

                    await using (var stream = await res.Content.ReadAsStreamAsync(cts.Token))
                    {
                        await using var fileToWriteTo = File.Open(t.Item2.TempFileName, FileMode.Create,
                            FileAccess.Write, FileShare.Read);
                        var buffer = new byte[BufferSize];
                        var sw = new Stopwatch();

                        while (true)
                        {
                            sw.Restart();
                            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cts.Token);
                            sw.Stop();

                            if (bytesRead == 0)
                                break;

                            await fileToWriteTo.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);

                            Interlocked.Add(ref downloadedBytesCount, bytesRead);

                            var elapsedTime = Math.Ceiling(sw.Elapsed.TotalSeconds);
                            var speed = CalculateDownloadSpeed(bytesRead, elapsedTime, SizeUnit.Kb);

                            tSpeed += speed;
                            cSpeed++;

                            downloadFile.OnChanged(
                                speed,
                                (double) downloadedBytesCount / responseLength,
                                downloadedBytesCount,
                                responseLength);
                        }

                        sw.Stop();
                    }

                    Interlocked.Add(ref tasksDone, 1);
                    doneRanges.TryTake(out _);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = downloadSettings.DownloadParts,
                    MaxDegreeOfParallelism = downloadSettings.DownloadParts,
                    CancellationToken = cts.Token
                });

                var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                var filesBlock =
                    new TransformManyBlock<IEnumerable<DownloadRange>, DownloadRange>(chunk => chunk,
                        new ExecutionDataflowBlockOptions());

                filesBlock.LinkTo(streamBlock, linkOptions);
                streamBlock.LinkTo(writeActionBlock, linkOptions);

                filesBlock.Post(readRanges);
                filesBlock.Complete();

                await writeActionBlock.Completion;

                var aSpeed = tSpeed / cSpeed;

                if (!doneRanges.IsEmpty)
                {
                    var ex = new AggregateException(new Exception("没有完全下载所有的分片"));

                    downloadFile.RetryCount++;
                    downloadFile.OnCompleted(false, ex, aSpeed);

                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    continue;
                }

                await using (var outputStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    using var hash = downloadSettings.GetHashAlgorithm();

                    foreach (var inputFilePath in readRanges)
                    {
                        await using var ms = new MemoryStream();

                        await using var inputStream = File.Open(inputFilePath.TempFileName, FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read);
                        outputStream.Seek(inputFilePath.Start, SeekOrigin.Begin);

                        await inputStream.CopyToAsync(outputStream, cts.Token);
                        await inputStream.CopyToAsync(ms, cts.Token);

                        if (downloadSettings.CheckFile)
                        {
                            var partBytes = ms.ToArray();
                            hash.TransformBlock(partBytes, 0, partBytes.Length, partBytes, 0);
                        }

                        File.Delete(inputFilePath.TempFileName);
                    }

                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                    if (downloadSettings.CheckFile && !string.IsNullOrEmpty(downloadFile.CheckSum))
                    {
                        var checkResult = CryptoHelper.ToString(hash.Hash)
                            .Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase);

                        if (!checkResult)
                        {
                            downloadFile.RetryCount++;
                            continue;
                        }
                    }
                }

                downloadFile.OnCompleted(true, null, aSpeed);

                streamBlock.Complete();
                writeActionBlock.Complete();

                #endregion

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

        downloadFile.OnCompleted(false, new AggregateException(exceptions), 0);
    }

    #endregion
}