using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Handler;
using SafeObjectPool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     下载帮助器。
    /// </summary>
    public static class DownloadHelper
    {
        private static int _downloadThread;

        /// <summary>
        ///     获取或设置用户代理信息。
        /// </summary>
        public static string Ua { get; set; } = "ProjBobcat";

        /// <summary>
        ///     下载线程
        /// </summary>
        public static int DownloadThread
        {
            get => _downloadThread;
            set
            {
                if (_downloadThread == value) return;

                _downloadThread = value;

                ClientsPool.Dispose();
                ClientsPool = GetObjectPool(value * 2);
            }
        }

        /// <summary>
        ///     最大重试计数
        /// </summary>
        public static int RetryCount { get; set; } = 5;

        private static ObjectPool<HttpClient> ClientsPool { get; set; } = GetObjectPool(30);

        private static ObjectPool<HttpClient> GetObjectPool(int num)
        {
            return new ObjectPool<HttpClient>(num,
                () =>
                {
                    var client = new HttpClient(new RetryHandler(new RedirectHandler(new HttpClientHandler
                    {
                        AllowAutoRedirect = false
                    }), RetryCount));

                    client.DefaultRequestHeaders.ConnectionClose = false;
                    client.Timeout = TimeSpan.FromMinutes(1.5);

                    return client;
                });
        }

        #region 下载数据

        /// <summary>
        ///     下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        public static async Task DownloadData(DownloadFile downloadProperty)
        {
            var filePath = Path.Combine(downloadProperty.DownloadPath, downloadProperty.FileName);
            using var client = await ClientsPool.GetAsync();

            try
            {
                using var request = new HttpRequestMessage {RequestUri = new Uri(downloadProperty.DownloadUri)};
                using var downloadTask = await client.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                    CancellationToken.None);

                // downloadTask.EnsureSuccessStatusCode();
                using var streamToRead = await downloadTask.Content.ReadAsStreamAsync();

                downloadProperty.Changed?.Invoke(null,
                    new DownloadFileChangedEventArgs
                    {
                        ProgressPercentage = 1,
                        BytesReceived = downloadTask.Content.Headers.ContentLength ?? 0,
                        TotalBytes = downloadTask.Content.Headers.ContentLength
                    });

                using (var fileToWriteTo = File.Open(filePath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    fileToWriteTo.Position = 0;
                    await streamToRead.CopyToAsync(fileToWriteTo, (int) streamToRead.Length, CancellationToken.None);
                }

                downloadProperty.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(true, null, downloadProperty));
            }
            catch (Exception e)
            {
                downloadProperty.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, e, downloadProperty));
            }
        }

        #endregion

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
                        {
                            Directory.CreateDirectory(df.DownloadPath);
                        }

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

        #region 分片下载

        /// <summary>
        ///     分片下载方法
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public static void MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 16)
        {
            MultiPartDownloadTaskAsync(downloadFile, numberOfParts).GetAwaiter().GetResult();
        }

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

            using var headClient = await ClientsPool.GetAsync();

            using var message = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));
            using var res1 = await headClient.Value.SendAsync(message);

            using var message2 = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));
            message2.Headers.Range =
                new RangeHeaderValue(0, Math.Max(res1.Content.Headers.ContentLength / 2 ?? 0, 1));

            using var res2 = await headClient.Value.SendAsync(message2);

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

            using var clientsPool = new ObjectPool<HttpClient>(10,
                () =>
                {
                    var client = new HttpClient(new RetryHandler(new RetryHandler(new RedirectHandler(
                            new HttpClientHandler
                            {
                                AllowAutoRedirect = false
                            })), RetryCount))
                        {MaxResponseContentBufferSize = responseLength};

                    client.DefaultRequestHeaders.ConnectionClose = false;
                    client.Timeout = TimeSpan.FromMinutes(5);

                    return client;
                });

            if (!Directory.Exists(downloadFile.DownloadPath))
                Directory.CreateDirectory(downloadFile.DownloadPath);

            #region Calculate ranges

            var readRanges = new List<DownloadRange>();
            var partSize = (long) Math.Round((double) responseLength / numberOfParts);
            var previous = 0L;

            if (partSize != 0)
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
                        using var wcObj = clientsPool.Get();

                        using var request = new HttpRequestMessage {RequestUri = new Uri(downloadFile.DownloadUri)};
                        request.Headers.ConnectionClose = false;
                        request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                        var downloadTask = wcObj.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                            CancellationToken.None);

                        doneRanges.Add(p);

                        var returnTuple = new Tuple<Task<HttpResponseMessage>, DownloadRange>(downloadTask, p);

                        return returnTuple;
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = DownloadThread,
                        MaxDegreeOfParallelism = DownloadThread
                    });

                var writeActionBlock = new ActionBlock<Tuple<Task<HttpResponseMessage>, DownloadRange>>(async t =>
                {
                    using var res = await t.Item1;
                    using var streamToRead = await res.Content.ReadAsStreamAsync();

                    using (var fileToWriteTo = File.Open(t.Item2.TempFileName, FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        fileToWriteTo.Position = 0;
                        await streamToRead.CopyToAsync(fileToWriteTo, (int) partSize, CancellationToken.None);
                    }

                    Interlocked.Add(ref tasksDone, 1);
                    Interlocked.Add(ref downloadedBytesCount, t.Item2.End - t.Item2.Start);

                    downloadFile.Changed?.Invoke(t,
                        new DownloadFileChangedEventArgs
                        {
                            ProgressPercentage = (double) downloadedBytesCount / responseLength,
                            BytesReceived = downloadedBytesCount,
                            TotalBytes = responseLength
                        });

                    doneRanges.TryTake(out _);

                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = DownloadThread,
                    MaxDegreeOfParallelism = DownloadThread
                });

                var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                var filesBlock =
                    new TransformManyBlock<IEnumerable<DownloadRange>, DownloadRange>(chunk => chunk,
                        new ExecutionDataflowBlockOptions());

                filesBlock.LinkTo(streamBlock, linkOptions);
                streamBlock.LinkTo(writeActionBlock, linkOptions);

                filesBlock.Post(readRanges);
                filesBlock.Complete();

                await writeActionBlock.Completion.ContinueWith(task =>
                {
                    if (!doneRanges.IsEmpty)
                    {
                        downloadFile.Completed?.Invoke(task,
                            new DownloadFileCompletedEventArgs(false, null, downloadFile));
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (FileNotFoundException)
                        {
                        }

                        return;
                    }

                    using var outputStream = File.Create(filePath);
                    foreach (var inputFilePath in readRanges)
                    {
                        using (var inputStream = File.OpenRead(inputFilePath.TempFileName))
                        {
                            outputStream.Position = inputFilePath.Start;
                            inputStream.CopyTo(outputStream);
                        }

                        File.Delete(inputFilePath.TempFileName);
                    }

                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(true, null, downloadFile));
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);

                #endregion
            }
            catch (Exception ex)
            {
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFile));

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