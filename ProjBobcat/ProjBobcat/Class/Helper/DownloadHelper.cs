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
using ProjBobcat.Event;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     下载帮助器。
    /// </summary>
    public static class DownloadHelper
    {
        /// <summary>
        ///     获取或设置用户代理信息。
        /// </summary>
        public static string Ua { get; set; } = "ProjBobcat";

        /// <summary>
        ///     下载线程
        /// </summary>
        public static int DownloadThread { get; set; }

        /// <summary>
        ///     最大重试计数
        /// </summary>
        public static int RetryCount { get; set; } = 10;

        private const int BufferSize = 1024 * 10 * 10;

        #region 下载一个列表中的文件（自动确定是否使用分片下载）

        /// <summary>
        ///     下载文件方法（自动确定是否使用分片下载）
        /// </summary>
        /// <param name="fileEnumerable">文件列表</param>
        /// <param name="downloadParts"></param>
        public static async Task AdvancedDownloadListFile(IEnumerable<DownloadFile> fileEnumerable,
            int downloadParts = 16)
        {
            ProcessorHelper.SetMaxThreads();

            var filesBlock =
                new TransformManyBlock<IEnumerable<DownloadFile>, DownloadFile>(d =>
                    {
                        var dl = d.ToList();

                        foreach (var df in dl.Where(df => !Directory.Exists(df.DownloadPath)))
                            Directory.CreateDirectory(df.DownloadPath);

                        return dl;
                    },
                    new ExecutionDataflowBlockOptions());

            var actionBlock = new ActionBlock<DownloadFile>(async d =>
            {
                if (d.FileSize >= 1048576 || d.FileSize == 0)
                    await MultiPartDownloadTaskAsync(d, downloadParts);
                else
                    await DownloadData(d);
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
        }

        #endregion

        #region 下载数据

        private static readonly HttpClient DataClient = HttpClientHelper.GetClient(RetryCount);

        /// <summary>
        ///     下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        public static async Task DownloadData(DownloadFile downloadProperty)
        {
            var filePath = Path.Combine(downloadProperty.DownloadPath, downloadProperty.FileName);

            try
            {
                using var request = new HttpRequestMessage {RequestUri = new Uri(downloadProperty.DownloadUri)};

                if (!string.IsNullOrEmpty(downloadProperty.Host))
                    request.Headers.Host = downloadProperty.Host;

                using var res = await DataClient.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                    CancellationToken.None);

                // downloadTask.EnsureSuccessStatusCode();

                await using var stream = await res.Content.ReadAsStreamAsync();
                await using var fileToWriteTo = File.Create(filePath);

                    var responseLength = res.Content.Headers.ContentLength ?? 0;
                var downloadedBytesCount = 0L;
                var buffer = new byte[BufferSize];
                var sw = new Stopwatch();
                sw.Start();

                var tSpeed = 0D;
                var cSpeed = 0;

                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize));

                    if (bytesRead == 0)
                        break;

                    await fileToWriteTo.WriteAsync(buffer.AsMemory(0, bytesRead));

                    Interlocked.Add(ref downloadedBytesCount, bytesRead);

                    var elapsedTime = sw.Elapsed.TotalSeconds;
                    var speed = (double) downloadedBytesCount / 1024 / 1024 / elapsedTime;

                    tSpeed += speed;
                    cSpeed++;

                    downloadProperty.Changed?.Invoke(null,
                        new DownloadFileChangedEventArgs
                        {
                            ProgressPercentage = (double)downloadedBytesCount / responseLength,
                            BytesReceived = downloadedBytesCount,
                            TotalBytes = responseLength,
                            Speed = speed
                        });
                }

                sw.Stop();
                fileToWriteTo.Close();

                var aSpeed = tSpeed / cSpeed;
                downloadProperty.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(true, null, downloadProperty, aSpeed));
            }
            catch (Exception e)
            {
                downloadProperty.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, e, downloadProperty, 0));
            }
        }

        #endregion

        #region 分片下载

        /// <summary>
        ///     分片下载方法
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public static void MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 16)
        {
            MultiPartDownloadTaskAsync(downloadFile, numberOfParts).Wait();
        }

        private static readonly HttpClient HeadClient = HttpClientHelper.GetClient(RetryCount);
        private static readonly HttpClient MultiPartClient = HttpClientHelper.GetClient(RetryCount);

        /// <summary>
        ///     分片下载方法（异步）
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public static async Task MultiPartDownloadTaskAsync(DownloadFile downloadFile, int numberOfParts = 16)
        {
            if (downloadFile == null) return;

            if (numberOfParts <= 0) numberOfParts = Environment.ProcessorCount;

            var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);

            #region Get file size

            using var message = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));
            if (!string.IsNullOrEmpty(downloadFile.Host))
                message.Headers.Host = downloadFile.Host;
            using var res1 = await HeadClient.SendAsync(message);

            using var message2 = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));
            message2.Headers.Range =
                new RangeHeaderValue(0, Math.Max(res1.Content.Headers.ContentLength / 2 ?? 0, 1));
            if (!string.IsNullOrEmpty(downloadFile.Host))
                message2.Headers.Host = downloadFile.Host;

            using var res2 = await HeadClient.SendAsync(message2);

            // res1.EnsureSuccessStatusCode();
            // res2.EnsureSuccessStatusCode();

            var responseLength = res1.Content.Headers.ContentLength ?? 0;
            var parallelDownloadSupported = res2.StatusCode == HttpStatusCode.PartialContent &&
                                            responseLength != 0;

            if (!parallelDownloadSupported)
            {
                await DownloadData(downloadFile);
                return;
            }

            #endregion

            if (!Directory.Exists(downloadFile.DownloadPath))
                Directory.CreateDirectory(downloadFile.DownloadPath);

            #region Calculate ranges

            var readRanges = new List<DownloadRange>();
            var partSize = (long) Math.Round((double) responseLength / numberOfParts);
            var previous = 0L;

            if (responseLength > numberOfParts)
                for (var i = (int) partSize; i <= responseLength; i += (int) partSize)
                    if (i + partSize < responseLength)
                    {
                        var start = previous;
                        var currentEnd = i;

                        readRanges.Add(new DownloadRange
                        {
                            Start = start,
                            End = currentEnd,
                            TempFileName = Path.GetTempFileName()
                        });

                        previous = currentEnd;
                    }
                    else
                    {
                        var start = previous;
                        var currentEnd = i;

                        readRanges.Add(new DownloadRange
                        {
                            Start = start,
                            End = responseLength,
                            TempFileName = Path.GetTempFileName()
                        });

                        previous = currentEnd;
                    }
            else
                readRanges.Add(new DownloadRange
                {
                    End = responseLength,
                    Start = 0,
                    TempFileName = Path.GetTempFileName()
                });

            #endregion

            try
            {
                #region Parallel download

                var downloadedBytesCount = 0L;
                var tasksDone = 0;
                var doneRanges = new ConcurrentBag<DownloadRange>();

                var streamBlock = new TransformBlock<DownloadRange, Tuple<Task<HttpResponseMessage>, DownloadRange>>(
                    p =>
                    {
                        using var request = new HttpRequestMessage {RequestUri = new Uri(downloadFile.DownloadUri)};
                        request.Headers.ConnectionClose = false;

                        if (!string.IsNullOrEmpty(downloadFile.Host))
                            request.Headers.Host = downloadFile.Host;

                        request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                        var downloadTask = MultiPartClient.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                            CancellationToken.None);

                        doneRanges.Add(p);

                        var returnTuple = new Tuple<Task<HttpResponseMessage>, DownloadRange>(downloadTask, p);

                        return returnTuple;
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = numberOfParts,
                        MaxDegreeOfParallelism = numberOfParts
                    });

                var tSpeed = 0D;
                var cSpeed = 0;

                var writeActionBlock = new ActionBlock<Tuple<Task<HttpResponseMessage>, DownloadRange>>(async t =>
                {
                    using var res = await t.Item1;

                    await using var stream = await res.Content.ReadAsStreamAsync();
                    await using var fileToWriteTo = File.OpenWrite(t.Item2.TempFileName);

                    var buffer = new byte[BufferSize];
                    var sw = new Stopwatch();
                    sw.Start();

                    while (true)
                    {
                        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize));
                        
                        if(bytesRead == 0)
                            break;

                        await fileToWriteTo.WriteAsync(buffer.AsMemory(0, bytesRead));

                        Interlocked.Add(ref downloadedBytesCount, bytesRead);

                        var elapsedTime = sw.Elapsed.TotalSeconds;
                        var speed = (double) downloadedBytesCount / 1024 / 1024 / elapsedTime;

                        tSpeed += speed;
                        cSpeed++;

                        downloadFile.Changed?.Invoke(t,
                            new DownloadFileChangedEventArgs
                            {
                                ProgressPercentage = (double)downloadedBytesCount / responseLength,
                                BytesReceived = downloadedBytesCount,
                                TotalBytes = responseLength,
                                Speed = speed
                            });
                    }
                    
                    sw.Stop();

                    //await res.Content.CopyToAsync(fileToWriteTo, CancellationToken.None);

                    Interlocked.Add(ref tasksDone, 1);
                    doneRanges.TryTake(out _);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = numberOfParts,
                    MaxDegreeOfParallelism = numberOfParts
                });

                var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                var filesBlock =
                    new TransformManyBlock<IEnumerable<DownloadRange>, DownloadRange>(chunk => chunk,
                        new ExecutionDataflowBlockOptions());

                filesBlock.LinkTo(streamBlock, linkOptions);
                streamBlock.LinkTo(writeActionBlock, linkOptions);

                filesBlock.Post(readRanges);
                filesBlock.Complete();

                await writeActionBlock.Completion.ContinueWith(async task =>
                {
                    var aSpeed = tSpeed / cSpeed;
                    
                    if (!doneRanges.IsEmpty)
                    {
                        var ex = task.Exception ?? new AggregateException(new Exception("没有完全下载所有的分片"));

                        downloadFile.Completed?.Invoke(task,
                            new DownloadFileCompletedEventArgs(false, ex, downloadFile, aSpeed));
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (FileNotFoundException)
                        {
                        }

                        return;
                    }

                    await using var outputStream = File.Create(filePath);
                    foreach (var inputFilePath in readRanges)
                    {
                        await using (var inputStream = File.OpenRead(inputFilePath.TempFileName))
                        {
                            outputStream.Seek(inputFilePath.Start, SeekOrigin.Begin);
                            await inputStream.CopyToAsync(outputStream);
                        }

                        File.Delete(inputFilePath.TempFileName);
                    }

                    outputStream.Close();
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(true, null, downloadFile, aSpeed));
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);

                #endregion
            }
            catch (Exception ex)
            {
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFile, 0));
                foreach (var piece in readRanges)
                    try
                    {
                        File.Delete(piece.TempFileName);
                    }
                    catch (FileNotFoundException)
                    {
                    }
            }
        }

        #endregion
    }
}