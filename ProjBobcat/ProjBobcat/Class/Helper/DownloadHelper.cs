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
        /// 设置用户代理信息。
        /// </summary>
        /// <param name="ua">要设置的用户代理信息。</param>
        [Obsolete("已过时，请使用属性 Ua 代替。")]
        public static void SetUa(string ua)
        {
            Ua = ua;
        }

        /// <summary>
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadToDir"></param>
        /// <param name="filename"></param>
        /// <param name="complete"></param>
        /// <param name="changedEvent"></param>
        public static async Task DownloadSingleFileAsyncWithEvent(
            Uri downloadUri, string downloadToDir, string filename,
            AsyncCompletedEventHandler complete,
            DownloadProgressChangedEventHandler changedEvent)
        {
            var di = new DirectoryInfo(downloadToDir);
            if (!di.Exists) di.Create();

            using var client = new WebClient {
                Timeout = 10000
            };
            client.Headers.Add("user-agent", Ua);
            client.DownloadFileCompleted += complete;
            client.DownloadProgressChanged += changedEvent;
            await client.DownloadFileTaskAsync(downloadUri, $"{downloadToDir}{filename}")
                .ConfigureAwait(false);
        }


        /// <summary>
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadToDir"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static async Task<TaskResult<string>> DownloadSingleFileAsync(
            Uri downloadUri, string downloadToDir, string filename)
        {
            var di = new DirectoryInfo(downloadToDir);
            if (!di.Exists) di.Create();

            using var client = new WebClient {
                Timeout = 10000
            };
            client.Headers.Add("user-agent", Ua);
            await client.DownloadFileTaskAsync(downloadUri, $"{downloadToDir}{filename}")
                .ConfigureAwait(false);
            return new TaskResult<string>(TaskResultStatus.Success);
        }


        #region 下载数据

        /// <summary>
        /// 下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        private static void DownloadData(DownloadFile downloadProperty)
        {
            using var client = new WebClient
            {
                Timeout = 10000
            };
            try
            {
                client.Headers.Add("user-agent", Ua);
                var result = client.DownloadData(new Uri(downloadProperty.DownloadUri));


                using var fs = new FileStream(downloadProperty.DownloadPath, FileMode.Create);
                fs.Write(result, 0, result.Length);
                fs.Close();

                downloadProperty.Completed?.Invoke(client,
                    new DownloadFileCompletedEventArgs(true, null, downloadProperty));
                downloadProperty.Changed?.Invoke(client, null);
            }
            catch (WebException ex)
            {
                if (File.Exists($"{downloadProperty.DownloadPath}{downloadProperty.FileName}"))
                    File.Delete($"{downloadProperty.DownloadPath}{downloadProperty.FileName}");
                downloadProperty.Completed?.Invoke(client,
                    new DownloadFileCompletedEventArgs(false, ex, downloadProperty));
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
                foreach (var df in downloadFiles) bc.Add(df, token);

                bc.CompleteAdding();
            }, token);

            using var downloadTask = Task.Run(() =>
            {
                void DownloadAction()
                {
                    foreach (var df in bc.GetConsumingEnumerable())
                    {
                        var di = new DirectoryInfo(
                            df.DownloadPath.Substring(0, df.DownloadPath.LastIndexOf('\\')));
                        if (!di.Exists) di.Create();

                         // DownloadData(df);
                         if (df.FileSize >= 1048576 || df.FileSize == 0)// || df.FileSize == default)
                             MultiPartDownload(df, downloadParts);
                         else
                             DownloadData(df);
                    }
                }

                var threads = new List<Thread>();

                for (var i = 0; i < downloadThread * 2; i++) threads.Add(new Thread(DownloadAction));

                foreach (var t in threads) t.Start();

                foreach (var t in threads) t.Join();
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
            if (downloadFile == null) return;

            //Handle number of parallel downloads  
            if (numberOfParts <= 0) numberOfParts = Environment.ProcessorCount;

            try
            {
                #region Get file size

                var webRequest = (HttpWebRequest) WebRequest.Create(new Uri(downloadFile.DownloadUri));
                webRequest.Method = "HEAD";
                webRequest.UserAgent = Ua;

                using var webResponse = webRequest.GetResponse();
                var parallelDownloadSupported = webResponse.Headers.Get("Accept-Ranges")?.Contains("bytes") ?? false;
                var responseLength = long.TryParse(webResponse.Headers.Get("Content-Length"), out var l) ? l : 0;
                parallelDownloadSupported = parallelDownloadSupported && responseLength != 0;

                if (!parallelDownloadSupported)
                {
                    DownloadData(downloadFile);
                    return;
                }

                #endregion

                if (File.Exists(downloadFile.DownloadPath)) File.Delete(downloadFile.DownloadPath);

                var tempFilesDictionary = new ConcurrentDictionary<int, string>();

                #region Calculate ranges

                var readRanges = new List<DownloadRange>();
                var partSize = (long) Math.Ceiling((double) responseLength / numberOfParts);

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

                #endregion

                #region Parallel download

                var totalProgress = Enumerable.Repeat(0d, numberOfParts).ToList();

                Parallel.ForEach(readRanges, (range, state) =>
                {
                    using var client = new WebClient
                    {
                        DownloadRange = range,
                        Timeout = Timeout.Infinite
                    };
                    client.DownloadProgressChanged += (sender, args) =>
                    {
                        lock (totalProgress)
                        {
                            totalProgress[range.Index] = (double)args.BytesReceived / args.TotalBytesToReceive;
                            downloadFile.Changed?.Invoke(sender,
                                new DownloadFileChangedEventArgs
                                    { ProgressPercentage = totalProgress.Sum() / numberOfParts });
                        }
                    };

                    var path = Path.GetTempFileName();
                    var t = client.DownloadFileTaskAsync(new Uri(downloadFile.DownloadUri), path);
                    t.GetAwaiter().GetResult();
                    tempFilesDictionary.TryAdd(range.Index, path);
                });

                #endregion

                #region Merge to single file

                if (tempFilesDictionary.Count != readRanges.Count)
                {
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(false, new HttpRequestException(), downloadFile));
                    return;
                }

                using var fs = new FileStream(downloadFile.DownloadPath, FileMode.Append);
                foreach (var downloadedBytes in tempFilesDictionary.OrderBy(b => b.Key))
                {
                    var wb = File.ReadAllBytes(downloadedBytes.Value);
                    fs.Write(wb, 0, wb.Length);
                    File.Delete(downloadedBytes.Value);
                }

                var totalLength = fs.Length;
                fs.Close();

                if (totalLength != responseLength)
                {
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(false, null, downloadFile));
                    return;
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
                    DownloadData(downloadFile);
                    return;
                }

                #endregion

                if (File.Exists(downloadFile.DownloadPath)) File.Delete(downloadFile.DownloadPath);

                var tempFilesDictionary = new ConcurrentDictionary<int,string>();

                #region Calculate ranges

                var readRanges = new List<DownloadRange>();
                var partSize = (long) Math.Ceiling((double) responseLength / numberOfParts);

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

                #endregion

                #region Parallel download

                var totalProgress = Enumerable.Repeat(0d, numberOfParts).ToList();

                Parallel.ForEach(readRanges, (range, state) =>
                {
                    using var client = new WebClient
                    {
                        DownloadRange = range,
                        Timeout = Timeout.Infinite
                    };
                    client.DownloadProgressChanged += (sender, args) =>
                    {
                        lock (totalProgress)
                        {
                            totalProgress[range.Index] = (double)args.BytesReceived / args.TotalBytesToReceive;
                            downloadFile.Changed?.Invoke(sender,
                                new DownloadFileChangedEventArgs
                                    { ProgressPercentage = totalProgress.Sum() / numberOfParts });
                        }
                    };

                    var path = Path.GetTempFileName();
                    var t = client.DownloadFileTaskAsync(new Uri(downloadFile.DownloadUri), path);
                    t.GetAwaiter().GetResult();
                    tempFilesDictionary.TryAdd(range.Index, path);
                });

                #endregion

                #region Merge to single file

                if (tempFilesDictionary.Count != readRanges.Count)
                {
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(false, new HttpRequestException(), downloadFile));
                    return;
                }

                using var fs = new FileStream(downloadFile.DownloadPath, FileMode.Append);
                foreach (var downloadedBytes in tempFilesDictionary.OrderBy(b => b.Key))
                {
                    var wb = File.ReadAllBytes(downloadedBytes.Value);
                    fs.Write(wb, 0, wb.Length);
                    File.Delete(downloadedBytes.Value);
                }

                var totalLength = fs.Length;
                fs.Close();

                if (totalLength != responseLength)
                {
                    downloadFile.Completed?.Invoke(null,
                        new DownloadFileCompletedEventArgs(false, null, downloadFile));
                    return;
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