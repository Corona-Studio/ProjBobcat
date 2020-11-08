using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
using ProjBobcat.Handler;
using SafeObjectPool;

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

        private static int _downloadThread;

        /// <summary>
        /// 下载线程
        /// </summary>
        public static int DownloadThread
        {
            get => _downloadThread;
            set
            {
                if (_downloadThread == value) return;

                _downloadThread = value;
                ClientsPool.Dispose();
                ClientsPool = new ObjectPool<HttpClient>(value,
                    () =>
                    {
                        var client = new HttpClient(new RetryHandler(new HttpClientHandler(), RetryCount));

                        client.DefaultRequestHeaders.ConnectionClose = false;
                        client.Timeout = TimeSpan.FromMinutes(5);

                        return client;
                    });
            }
        }

        /// <summary>
        /// 最大重试计数
        /// </summary>
        public static int RetryCount { get; set; } = 10;

        private static ObjectPool<HttpClient> ClientsPool { get; set; } = new ObjectPool<HttpClient>(10,
            () =>
            {
                var client = new HttpClient(new RetryHandler(new HttpClientHandler(), RetryCount));

                client.DefaultRequestHeaders.ConnectionClose = false;
                client.Timeout = TimeSpan.FromMinutes(5);

                return client;
            });

        /// <summary>
        ///     异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadDir"></param>
        /// <param name="filename"></param>
        /// <param name="complete"></param>
        /// <param name="changedEvent"></param>
        public static async Task DownloadSingleFileAsyncWithEvent(
            Uri downloadUri, string downloadDir, string filename,
            AsyncCompletedEventHandler complete,
            DownloadProgressChangedEventHandler changedEvent)
        {
            var di = new DirectoryInfo(downloadDir);
            if (!di.Exists) di.Create();

            using var wc = new WebClient();
            wc.Headers.Add("user-agent", Ua);
            wc.DownloadFileCompleted += complete;
            wc.DownloadProgressChanged += changedEvent;
            await wc.DownloadFileTaskAsync(downloadUri, Path.Combine(downloadDir, filename)).ConfigureAwait(false);
        }


        /// <summary>
        ///     异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadDir"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static async Task<TaskResult<string>> DownloadSingleFileAsync(
            Uri downloadUri, string downloadDir, string filename)
        {
            var di = new DirectoryInfo(downloadDir);
            if (!di.Exists) di.Create();

            try
            {
                using var wc = new WebClient();
                wc.Headers.Add(HttpRequestHeader.UserAgent, Ua);

                await wc.DownloadFileTaskAsync(downloadUri, Path.Combine(downloadDir, filename)).ConfigureAwait(false);
                return new TaskResult<string>(TaskResultStatus.Success);
            }
            catch (Exception ex)
            {
                return new TaskResult<string>(TaskResultStatus.Error, ex.GetBaseException().Message);
            }
        }


        #region 下载数据

        /// <summary>
        ///     下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        private static async Task DownloadData(DownloadFile downloadProperty)
        {
            var filePath = Path.Combine(downloadProperty.DownloadPath, downloadProperty.FileName);
            using var wcObj = ClientsPool.Get();

            try
            {
                using var request = new HttpRequestMessage { RequestUri = new Uri(downloadProperty.DownloadUri) };
                var downloadTask = wcObj.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                    CancellationToken.None);

                using var streamToRead = await downloadTask.Result.Content.ReadAsStreamAsync();

                downloadProperty.Changed?.Invoke(null,
                    new DownloadFileChangedEventArgs
                    {
                        ProgressPercentage = 1,
                        BytesReceived = downloadTask.Result.Content.Headers.ContentLength ?? 0,
                        TotalBytes = downloadTask.Result.Content.Headers.ContentLength
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
                new TransformManyBlock<IEnumerable<DownloadFile>, DownloadFile>(d => d,
                    new ExecutionDataflowBlockOptions());

            var actionBlock = new ActionBlock<DownloadFile>(async d =>
            {
                if (!Directory.Exists(d.DownloadPath)) Directory.CreateDirectory(d.DownloadPath);

                if (d.FileSize >= 1048576 || d.FileSize == 0)
                    await MultiPartDownloadTaskAsync(d, downloadParts);
                else
                    await DownloadData(d);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Environment.ProcessorCount,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            });

            var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};
            filesBlock.LinkTo(actionBlock, linkOptions);
            filesBlock.Post(fileEnumerable.ToList());
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

            var response = await WebRequest.Create(new Uri(downloadFile.DownloadUri)).GetResponseAsync()
                .ConfigureAwait(false);
            var responseLength = response.ContentLength;
            var parallelDownloadSupported = (response.Headers.Get("Accept-Ranges")?.Contains("bytes") ?? false) &&
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
                    var client = new HttpClient(new RetryHandler(new HttpClientHandler(), RetryCount))
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

                        var request = new HttpRequestMessage {RequestUri = new Uri(downloadFile.DownloadUri)};
                        request.Headers.ConnectionClose = false;
                        request.Headers.Range = new RangeHeaderValue(p.Start, p.End);

                        var downloadTask = wcObj.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                            CancellationToken.None);

                        doneRanges.Add(p);

                        var returnTuple = new Tuple<Task<HttpResponseMessage>, DownloadRange>(downloadTask, p);

                        return returnTuple;
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = Environment.ProcessorCount,
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    });

                var writeActionBlock = new ActionBlock<Tuple<Task<HttpResponseMessage>, DownloadRange>>(async t =>
                {
                    using var streamToRead = await t.Item1.Result.Content.ReadAsStreamAsync();
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
                    BoundedCapacity = Environment.ProcessorCount,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
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