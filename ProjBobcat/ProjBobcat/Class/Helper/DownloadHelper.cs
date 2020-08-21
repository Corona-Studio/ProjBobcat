using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// 下载帮助器。
    /// </summary>
    public class DownloadHelper : IDisposable
    {
        private static DownloadHelper _instance;
        public static DownloadHelper Instance
        {
            get
            {
                if(_instance == null)
                    Init();
                return _instance;
            }
            private set => _instance = value;
        }

        public static void Init()
        {
            var hCh = new HttpClientHandler();
            var pMh = new ProgressMessageHandler(hCh);

            Instance = new DownloadHelper
            {
                _httpClientHandler = hCh,
                _progressMessageHandler = pMh,
                _httpClient = new HttpClient(pMh),
                Ua = "ProjBobcat"
            };
        }

        private HttpClientHandler _httpClientHandler;
        private ProgressMessageHandler _progressMessageHandler;
        private HttpClient _httpClient;

        /// <summary>
        /// 获取或设置用户代理信息。
        /// </summary>
        private string _ua;
        public string Ua
        {
            get => _ua;
            set
            {
                _ua = value;
                _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);
            }
        }

        /// <summary>
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadDir"></param>
        /// <param name="filename"></param>
        /// <param name="complete"></param>
        /// <param name="changedEvent"></param>
        public async Task DownloadSingleFileAsyncWithEvent(
            Uri downloadUri, string downloadDir, string filename,
            AsyncCompletedEventHandler complete,
            EventHandler<HttpProgressEventArgs> changedEvent)
        {
            var di = new DirectoryInfo(downloadDir);
            if (!di.Exists) di.Create();

            try
            {
                _progressMessageHandler.HttpReceiveProgress += changedEvent;
                var bytes = await _httpClient.GetByteArrayAsync(downloadUri).ConfigureAwait(false);
                using var fs = new FileStream(Path.Combine(downloadDir, filename), FileMode.Create);
                using var bw = new BinaryWriter(fs);

                bw.Write(bytes);

                bw.Close();
                fs.Close();

                complete?.Invoke(this, new AsyncCompletedEventArgs(null, false, null));
            }
            catch(Exception ex)
            {
                complete?.Invoke(this, new AsyncCompletedEventArgs(ex, false, null));
            }
            finally
            {
                _progressMessageHandler.HttpReceiveProgress -= changedEvent;
            }
        }


        /// <summary>
        /// 异步下载单个文件。
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadDir"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public async Task<TaskResult<string>> DownloadSingleFileAsync(
            Uri downloadUri, string downloadDir, string filename)
        {
            var di = new DirectoryInfo(downloadDir);
            if (!di.Exists) di.Create();

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(downloadUri).ConfigureAwait(false);
                using var fs = new FileStream(Path.Combine(downloadDir, filename), FileMode.Create);
                using var bw = new BinaryWriter(fs);

                bw.Write(bytes);

                bw.Close();
                fs.Close();

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
        private async Task DownloadData(DownloadFile downloadProperty)
        {
            void ReceivedProcessor(object sender, HttpProgressEventArgs args)
            {
                downloadProperty.Changed?.Invoke(this, new DownloadFileChangedEventArgs
                {
                    ProgressPercentage = (double)args.BytesTransferred / args.TotalBytes * 100 ?? 0,
                    BytesReceived = args.BytesTransferred,
                    TotalBytes = args.TotalBytes
                });
            }

            _progressMessageHandler.HttpReceiveProgress += ReceivedProcessor;

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(new Uri(downloadProperty.DownloadUri))
                    .ConfigureAwait(false);
                using var fs = new FileStream(
                    Path.Combine(downloadProperty.DownloadPath, downloadProperty.FileName),
                    FileMode.Create);
                using var bw = new BinaryWriter(fs);

                bw.Write(bytes);

                bw.Close();
                fs.Close();

                downloadProperty.Completed?.Invoke(this,
                    new DownloadFileCompletedEventArgs(true, null, downloadProperty));
            }
            catch (Exception e)
            {
                downloadProperty.Completed?.Invoke(this,
                    new DownloadFileCompletedEventArgs(false, e, downloadProperty));
            }
            finally
            {
                _progressMessageHandler.HttpReceiveProgress -= ReceivedProcessor;
                if (File.Exists(downloadProperty.DownloadPath))
                    File.Delete(downloadProperty.DownloadPath);
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
        public async Task AdvancedDownloadListFile(IEnumerable<DownloadFile> fileEnumerable, int downloadThread,
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
                downloadFiles.AsParallel().ForAll(d => bc.Add(d, token));

                bc.CompleteAdding();
            }, token);

            using var downloadTask = Task.Run(async () =>
            {
                void DownloadAction()
                {
                    foreach (var df in bc.GetConsumingEnumerable())
                    {
                        var di = new DirectoryInfo(
                            df.DownloadPath.Substring(0, df.DownloadPath.LastIndexOf('\\')));
                        if (!di.Exists) di.Create();

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
        public void MultiPartDownload(DownloadFile downloadFile, int numberOfParts = 16)
        {
            MultiPartDownloadTaskAsync(downloadFile, numberOfParts).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 分片下载方法（异步）
        /// </summary>
        /// <param name="downloadFile">下载文件信息</param>
        /// <param name="numberOfParts">分段数量</param>
        public async Task MultiPartDownloadTaskAsync(DownloadFile downloadFile, int numberOfParts = 16)
        {
            if (downloadFile == null) return;

            //Handle number of parallel downloads  
            if (numberOfParts <= 0) numberOfParts = Environment.ProcessorCount;

            try
            {
                #region Get file size

                using var hRm = new HttpRequestMessage(HttpMethod.Head,
                    new Uri(downloadFile.DownloadUri));
                var response = await _httpClient.SendAsync(hRm).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var parallelDownloadSupported = response.Headers.AcceptRanges.Any(ar => ar.Contains("bytes"));
                var responseLength = response.Content.Headers.ContentLength ?? 0;
                parallelDownloadSupported = parallelDownloadSupported && responseLength != 0;

                if (!parallelDownloadSupported)
                {
                    DownloadData(downloadFile).GetAwaiter().GetResult();
                    return;
                }

                #endregion

                if (File.Exists(downloadFile.DownloadPath)) File.Delete(downloadFile.DownloadPath);

                var tempFilesBag = new ConcurrentBag<Tuple<int, string>>();

                #region Calculate ranges

                var readRanges = new List<DownloadRange>();
                var partSize = (long) Math.Ceiling((double) responseLength / numberOfParts);

                if(partSize != 0)
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
                    var range = readRanges[i];

                    void DownloadMethod()
                    {
                        var lastReceivedBytes = 0L;
                        
                        void ReceivedProcessor(object sender, HttpProgressEventArgs args)
                        {
                            downloadedBytesCount += args.BytesTransferred - lastReceivedBytes;
                            lastReceivedBytes = args.BytesTransferred;

                            downloadFile.Changed?.Invoke(sender,
                                new DownloadFileChangedEventArgs
                                    {ProgressPercentage = (double) downloadedBytesCount / responseLength});
                        }

                        _progressMessageHandler.HttpReceiveProgress += ReceivedProcessor;
                        var path = Path.GetTempFileName();

                        try
                        {
                            var bytes = _httpClient.GetByteArrayAsync(new Uri(downloadFile.DownloadUri)).GetAwaiter()
                                .GetResult();
                            using var fss = new FileStream(path, FileMode.Create);
                            using var bw = new BinaryWriter(fss);

                            bw.Write(bytes);

                            bw.Close();
                            fss.Close();
                        }
                        finally
                        {
                            _progressMessageHandler.HttpReceiveProgress -= ReceivedProcessor;
                        }

                        tempFilesBag.Add(new Tuple<int, string>(range.Index, path));
                    }

                    var t = new Task(DownloadMethod);
                    tasks[i] = t;
                }

                foreach (var t in tasks)
                {
                    t.Start();
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

                using var fs = new FileStream(Path.Combine(downloadFile.DownloadPath, downloadFile.FileName),
                    FileMode.Append);
                foreach (var element in tempFilesBag.ToArray().OrderBy(b => b.Item1).ToArray())
                {
                    var wb = File.ReadAllBytes(element.Item2);
                    fs.Write(wb, 0, wb.Length);
                    File.Delete(element.Item2);
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

        public void Dispose() { }
    }
}