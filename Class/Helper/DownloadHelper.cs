using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper
{
    public static class DownloadHelper
    {
        private static string Ua { get; set; } = "ProjBobcat";

        public static void SetUa(string ua)
        {
            Ua = ua;
        }

        #region 下载方法（异步，单个文件）

        /// <summary>
        ///     下载单个文件
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadPath"></param>
        /// <param name="filename"></param>
        /// <param name="complete"></param>
        /// <param name="changedEvent"></param>
        public static async Task DownloadSingleFileAsyncWithEvent(Uri downloadUri, string downloadPath,
            string filename,
            AsyncCompletedEventHandler complete = null, DownloadProgressChangedEventHandler changedEvent = null)
        {
            var di = new DirectoryInfo(downloadPath);
            if (!di.Exists) di.Create();

            #region 下载代码

            using var client = new WebClient
            {
                Timeout = 10000
            };
            client.Headers.Add("user-agent", Ua);
            client.DownloadFileCompleted += complete;
            client.DownloadProgressChanged += changedEvent;
            await client.DownloadFileTaskAsync(downloadUri, $"{downloadPath}{filename}")
                .ConfigureAwait(false);

            #endregion
        }

        #endregion

        #region 下载单一文件（异步且没有完成事件）

        public static async Task<TaskResult<string>> DownloadSingleFileAsync(Uri downloadUri, string downloadPath,
            string filename)
        {
            var di = new DirectoryInfo(downloadPath);
            if (!di.Exists) di.Create();

            #region 下载代码

            using var client = new WebClient
            {
                Timeout = 10000
            };
            client.Headers.Add("user-agent", Ua);
            //client.DownloadFileCompleted += FileDownloadCompleted;
            await client.DownloadFileTaskAsync(downloadUri, $"{downloadPath}{filename}")
                .ConfigureAwait(false);
            return new TaskResult<string>(TaskResultStatus.Success);

            #endregion
        }

        #endregion

        #region 下载数据

        /// <summary>
        ///     下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        private static void DownloadData(DownloadFile downloadProperty)
        {
            #region 下载代码

            using var client = new WebClient
            {
                Timeout = 10000
            };
            try
            {
                client.Headers.Add("user-agent", Ua);
                var result = client.DownloadData(new Uri(downloadProperty.DownloadUri));
                using var stream = new MemoryStream(result);

                FileHelper.SaveBinaryFile(stream, downloadProperty.DownloadPath);

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

            #endregion
        }

        #endregion

        #region 分片下载

        public static void MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 8)
        {
            if (downloadFile == null) return;

            //Handle number of parallel downloads  
            if (numberOfParts <= 0)
            {
                numberOfParts = Environment.ProcessorCount;
            }

            try
            {
                #region Get file size

                var webRequest = WebRequest.Create(new Uri(downloadFile.DownloadUri));
                webRequest.Method = "HEAD";
                long responseLength;
                bool parallelDownloadSupported;

                using (var webResponse = webRequest.GetResponse())
                {
                    parallelDownloadSupported = webResponse.Headers.Get("Accept-Ranges").Contains("bytes");
                    responseLength = long.TryParse(webResponse.Headers.Get("Content-Length"), out var l) ? l : 0;
                }

                if (!parallelDownloadSupported)
                {
                    DownloadData(downloadFile);
                    return;
                }

                #endregion

                if (File.Exists(downloadFile.DownloadPath))
                {
                    File.Delete(downloadFile.DownloadPath);
                }

                var tempFilesDictionary = new ConcurrentDictionary<int, byte[]>();

                #region Calculate ranges

                var readRanges = new List<DownloadRange>();
                var partSize = (long)Math.Ceiling((double)responseLength / numberOfParts);

                for (var i = 0; i < numberOfParts; i++)
                {
                    var start = i * partSize + Math.Min(1, i);
                    var end = Math.Min((i + 1) * partSize, responseLength);

                    readRanges.Add(new DownloadRange
                    {
                        End = end,
                        Start = start
                    });
                }

                #endregion

                #region Parallel download

                var index = 0;
                Parallel.ForEach(readRanges, new ParallelOptions {MaxDegreeOfParallelism = numberOfParts},
                    delegate(DownloadRange range)
                    {
                        var client = new WebClient
                        {
                            DownloadRange = range
                        };

                        var data = client.DownloadData(new Uri(downloadFile.DownloadUri));

                        if (!tempFilesDictionary.TryAdd(index, data))
                        {
                            downloadFile.Completed?.Invoke(null,
                                new DownloadFileCompletedEventArgs(false, null, downloadFile));
                            return;
                        }

                        index++;
                    });

                #endregion

                #region Merge to single file

                var bytes = new List<byte>();
                foreach (var downloadedBytes in tempFilesDictionary.OrderBy(b => b.Key))
                {
                    bytes.AddRange(downloadedBytes.Value);
                }

                using var s = new MemoryStream(bytes.ToArray());
                FileHelper.SaveBinaryFile(s, downloadFile.DownloadPath);

                #endregion

                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(true, null, downloadFile));
                downloadFile.Changed?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFile));
            }
        }

        /*
        public static async Task MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 8)
        {
            if (downloadFile == null) return;

            try
            {
                using var httpClient = new HttpClient();
                using var message = new HttpRequestMessage(HttpMethod.Head, new Uri(downloadFile.DownloadUri));
                var response = await httpClient.SendAsync(message).ConfigureAwait(false);
                var parallelDownloadSupported = response.Headers.AcceptRanges?.Contains("bytes") ?? false;
                var contentLength = response.Content.Headers.ContentLength ?? 0;

                if (!parallelDownloadSupported)
                {
                    DownloadData(downloadFile);
                    return;
                }

                var tasks = new List<Task>();
                var partSize = (long)Math.Ceiling((double)contentLength / numberOfParts);

                File.Create(downloadFile.DownloadPath).Dispose();

                for (var i = 0; i < numberOfParts; i++)
                {
                    var start = i * partSize + Math.Min(1, i);
                    var end = Math.Min((i + 1) * partSize, contentLength);

                    tasks.Add(Task.Run(async () =>
                    {
                        await DownloadPart(downloadFile, start, end).ConfigureAwait(false);
                    }));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(true, null, downloadFile));
                downloadFile.Changed?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                downloadFile.Completed?.Invoke(null,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFile));
            }
        }

        private static async Task DownloadPart(DownloadFile downloadFile, long start, long end)
        {
            using var httpClient = new HttpClient();
            using var fileStream = new FileStream(downloadFile.DownloadPath, FileMode.Open, FileAccess.Write, FileShare.Write);
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(downloadFile.DownloadUri));
                message.Headers.Add("Range", $"bytes={start}-{end}");

                fileStream.Position = start;
                await httpClient.SendAsync(message).Result.Content.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }
        */

        #endregion

        #region 下载一个列表中的文件（自动确定是否使用分片下载）

        /// <summary>
        ///     下载文件方法（自动确定是否使用分片下载）
        /// </summary>
        /// <param name="fileEnumerable">文件列表</param>
        /// <param name="tokenSource"></param>
        public static async Task AdvancedDownloadListFile(IEnumerable<DownloadFile> fileEnumerable,
        CancellationTokenSource tokenSource)
        {
            var downloadFiles = fileEnumerable.ToList();
            var token = tokenSource?.Token ?? CancellationToken.None;
            var processorCount = ProcessorHelper.GetPhysicalProcessorCount();

            using var bc = new BlockingCollection<DownloadFile>(processorCount * 4);
            using var downloadQueueTask = Task.Run(() =>
            {
                foreach (var df in downloadFiles)
                {
                    bc.Add(df, token);
                }

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

                        DownloadData(df);
                        /*
                        if (df.FileSize >= 1048576 || df.FileSize == 0 || df.FileSize == default)
                        {
                            MultiPartDownload(df);
                        }
                        else
                        {
                            
                        }
                        */
                    }
                }

                var threads = new List<Thread>();

                for (var i = 0; i < processorCount * 2; i++)
                {
                    threads.Add(new Thread(DownloadAction));
                }

                foreach (var t in threads)
                {
                    t.Start();
                }

                foreach (var t in threads)
                {
                    t.Join();
                }
            }, token);

            await Task.WhenAll(downloadQueueTask, downloadTask).ConfigureAwait(false);
        }

        #endregion
    }
}