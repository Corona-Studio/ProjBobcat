using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// 下载帮助器。
    /// </summary>
    public static class DownloadHelper
    {
        /// <summary>
        /// 获取或设置用户代理信息。
        /// </summary>
        public static string Ua { get; set; } = "ProjBobcat";

        /// <summary>
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadToDir"></param>
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
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadToDir"></param>
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
        /// 下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        private static async Task DownloadData(DownloadFile downloadProperty)
        {
            var filePath = Path.Combine(downloadProperty.DownloadPath, downloadProperty.FileName);
            
            try
            {
                using var wc = new WebClient();
                wc.Headers.Add(HttpRequestHeader.UserAgent, Ua);

                wc.DownloadProgressChanged += (sender, args) =>
                {
                    downloadProperty.Changed?.Invoke(sender,
                        new DownloadFileChangedEventArgs
                        {
                            ProgressPercentage = (double) args.BytesReceived / args.TotalBytesToReceive,
                            BytesReceived = args.BytesReceived,
                            TotalBytes = args.TotalBytesToReceive
                        });
                };

                await wc.DownloadFileTaskAsync(downloadProperty.DownloadUri, filePath).ConfigureAwait(false);

                downloadProperty.Completed?.Invoke(wc,
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
        /// <param name="downloadThread">下载线程</param>
        /// <param name="tokenSource"></param>
        /// <param name="downloadParts"></param>
        public static async Task AdvancedDownloadListFile(IEnumerable<DownloadFile> fileEnumerable, int downloadThread,
            CancellationTokenSource tokenSource, int downloadParts = 16)
        {
            var downloadFiles = fileEnumerable.ToList();
            var token = tokenSource?.Token ?? CancellationToken.None;
            var processorCount = ProcessorHelper.GetPhysicalProcessorCount();

            if (downloadThread <= 0)
                downloadThread = processorCount;

            using var bc = new BlockingCollection<DownloadFile>(downloadThread * 4);
            using var downloadQueueTask = Task.Run(() =>
            {
                downloadFiles.ForEach(d => bc.Add(d, token));

                bc.CompleteAdding();
            }, token);

            using var downloadTask = Task.Run(async () =>
            {
                void DownloadAction()
                {
                    foreach (var df in bc.GetConsumingEnumerable())
                    {
                        if (!Directory.Exists(df.DownloadPath)) Directory.CreateDirectory(df.DownloadPath);

                         if (df.FileSize >= 1048576 || df.FileSize == 0)
                             MultiPartDownload(df, downloadParts);
                         else
                             DownloadData(df).GetAwaiter().GetResult();
                    }
                }

                var tasks = new Task[downloadThread * 2];

                for (var i = 0; i < downloadThread * 2; i++)
                {
                    tasks[i] = new Task(DownloadAction);
                    tasks[i].Start();
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }, token);

            await Task.WhenAll(downloadQueueTask, downloadTask).ConfigureAwait(false);
        }

        #endregion

        #region 分片下载

        /// <summary>
        /// 分片下载方法
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public static void MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 16)
        {
            MultiPartDownloadTaskAsync(downloadFile, numberOfParts).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 分片下载方法（异步）
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public static async Task MultiPartDownloadTaskAsync(DownloadFile downloadFile, int numberOfParts = 16)
        {
            if (downloadFile == null) return;

            //Handle number of parallel downloads  
            if (numberOfParts <= 0) numberOfParts = Environment.ProcessorCount;

            var filePath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);

            try
            {
                #region Get file size

                var webRequest = (HttpWebRequest) WebRequest.Create(new Uri(downloadFile.DownloadUri));
                webRequest.Method = "HEAD";
                webRequest.UserAgent = Ua;
                long responseLength;
                bool parallelDownloadSupported;
                
                using (var webResponse = await webRequest.GetResponseAsync().ConfigureAwait(false))
                {
                    parallelDownloadSupported = webResponse.Headers.Get("Accept-Ranges")?.Contains("bytes") ?? false;
                    responseLength = long.TryParse(webResponse.Headers.Get("Content-Length"), out var l) ? l : 0;
                    parallelDownloadSupported = parallelDownloadSupported && responseLength != 0;
                }

                if (!parallelDownloadSupported)
                {
                    DownloadData(downloadFile).GetAwaiter().GetResult();
                    return;
                }

                #endregion

                if (!Directory.Exists(downloadFile.DownloadPath))
                    Directory.CreateDirectory(downloadFile.DownloadPath);
                if (File.Exists(filePath)) File.Delete(filePath);

                var tempFilesBag = new ConcurrentBag<Tuple<int, string>>();

                #region Calculate ranges

                var readRanges = new List<DownloadRange>();
                var partSize = (long) Math.Ceiling((double) responseLength / numberOfParts);

                if (partSize != 0)
                    for (var i = 0; i < numberOfParts; i++)
                    {
                        var start = i * partSize + Math.Min(1, i);
                        var end = Math.Min((i + 1) * partSize, responseLength);

                        readRanges.Add(new DownloadRange
                        {
                            End = end,
                            Start = start,
                            Index = i
                        });
                    }
                else
                    readRanges.Add(new DownloadRange
                    {
                        End = responseLength,
                        Start = 0,
                        Index = 0
                    });

                #endregion

                #region Parallel download

                var downloadedBytesCount = 0L;

                var tasks = new Task[readRanges.Count];

                for (var i = 0; i < readRanges.Count; i++)
                {
                    void DownloadMethod(object indexNum)
                    {
                        var range = readRanges[(int) indexNum];
                        var path = Path.GetTempFileName();
                        var lastDownloadedBytesCount = 0L;

                        using var wc = new WebClient
                        {
                            DownloadRange = range,
                            Timeout = Timeout.Infinite
                        };

                        wc.Headers.Add(HttpRequestHeader.UserAgent, Ua);

                        wc.DownloadProgressChanged += (sender, args) =>
                        {
                            downloadedBytesCount += args.BytesReceived - lastDownloadedBytesCount;
                            lastDownloadedBytesCount = args.BytesReceived;

                            downloadFile.Changed?.Invoke(sender,
                                new DownloadFileChangedEventArgs
                                {
                                    ProgressPercentage = (double) downloadedBytesCount / responseLength,
                                    BytesReceived = downloadedBytesCount,
                                    TotalBytes = responseLength
                                });
                        };

                        wc.DownloadFileTaskAsync(new Uri(downloadFile.DownloadUri), path).GetAwaiter().GetResult();

                        tempFilesBag.Add(new Tuple<int, string>(range.Index, path));

                    }

                    var t = new Task(DownloadMethod, i);
                    tasks[i] = t;
                    tasks[i].Start();
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                #endregion

                #region Merge to single file

                if (tempFilesBag.Count != readRanges.Count)
                {
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(false, new HttpRequestException(), downloadFile));
                    return;
                }

                using var fs = new FileStream(filePath, FileMode.Append);
                foreach (var element in tempFilesBag.OrderBy(b => b.Item1))
                {
                    var wb = File.ReadAllBytes(element.Item2);
                    fs.Write(wb, 0, wb.Length);
                    File.Delete(element.Item2);
                }
                #endregion

                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(true, null, downloadFile));
            }
            catch (Exception ex)
            {
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFile));
            }
        }

        #endregion
    }
}