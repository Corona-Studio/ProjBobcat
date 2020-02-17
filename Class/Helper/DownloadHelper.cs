using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

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
            if(!di.Exists) di.Create();

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

        public static Task<TaskResult<string>> DownloadSingleFileAsync(Uri downloadUri, string downloadPath,
            string filename)
        {
            var di = new DirectoryInfo(downloadPath);
            if (!di.Exists) di.Create();

            #region 下载代码

            return Task.Run(async () =>
            {
                using var client = new WebClient
                {
                    Timeout = 10000
                };
                client.Headers.Add("user-agent", Ua);
                //client.DownloadFileCompleted += FileDownloadCompleted;
                await client.DownloadFileTaskAsync(downloadUri, $"{downloadPath}{filename}")
                    .ConfigureAwait(false);
                return new TaskResult<string>(TaskResultStatus.Success);
            });

            #endregion
        }

        #endregion

        #region 下载数据

        /// <summary>
        ///     下载文件（通过线程池）
        /// </summary>
        /// <param name="downloadProperty"></param>
        private static void DownloadData(object downloadProperty)
        {
            var downloadFileProperty = (DownloadFile)downloadProperty;

            var di = new DirectoryInfo(downloadFileProperty.DownloadPath);
            if (!di.Exists) di.Create();

            #region 下载代码

            using var client = new WebClient
            {
                Timeout = 10000
            };
            try
            {
                client.Headers.Add("user-agent", Ua);
                var result = client.DownloadData(new Uri(downloadFileProperty.DownloadUri));
                using var stream = new MemoryStream(result);

                FileHelper.SaveBinaryFile(stream,
                    $"{downloadFileProperty.DownloadPath}{downloadFileProperty.FileName}");

                downloadFileProperty.Completed?.Invoke(client,
                    new DownloadFileCompletedEventArgs(true, null, downloadFileProperty));
                downloadFileProperty.Changed?.Invoke(client, new DownloadFileChangedEventArgs());
            }
            catch (WebException ex)
            {
                if (File.Exists($"{downloadFileProperty.DownloadPath}{downloadFileProperty.FileName}"))
                    File.Delete($"{downloadFileProperty.DownloadPath}{downloadFileProperty.FileName}");
                downloadFileProperty.Completed?.Invoke(client,
                    new DownloadFileCompletedEventArgs(false, ex, downloadFileProperty));
            }

            #endregion
        }

        #endregion

        #region 下载一个列表中的文件

        /// <summary>
        ///     下载文件方法
        /// </summary>
        /// <param name="fileEnumerable">文件列表</param>
        /// <param name="tokenSource"></param>
        public static async Task DownloadListFile(IEnumerable<DownloadFile> fileEnumerable,
            CancellationTokenSource tokenSource)
        {
            var downloadFiles = fileEnumerable.ToList();
            var token = tokenSource?.Token ?? CancellationToken.None;

            using var bc = new BlockingCollection<DownloadFile>(128);

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
                        DownloadData(df);
                    }
                }

                var threads = new List<Thread>();

                for (var i = 0; i < 64; i++)
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