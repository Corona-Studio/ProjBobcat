using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent
{
#nullable enable
    /// <summary>
    ///     默认的资源补全器
    /// </summary>
    public class DefaultResourceCompleter : IResourceCompleter
    {
        ConcurrentBag<DownloadFile> _retryFiles;

        public DefaultResourceCompleter()
        {
            _retryFiles = new ConcurrentBag<DownloadFile>();
        }

        public int TotalDownloaded { get; set; }
        public int NeedToDownload { get; set; }

        public int DownloadParts { get; set; } = 16;
        public int TotalRetry { get; set; }
        public bool CheckFile { get; set; }
        public IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }


        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;

        public TaskResult<ResourceCompleterCheckResult?> CheckAndDownload()
        {
            return CheckAndDownloadTaskAsync().Result;
        }

        public async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync()
        {
            _retryFiles.Clear();

            if (!(ResourceInfoResolvers?.Any() ?? false))
                return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

            var totalLostFiles = new List<IGameResource>();
            foreach (var resolver in ResourceInfoResolvers)
            {
                resolver.GameResourceInfoResolveEvent += GameResourceInfoResolveStatus;

                await foreach (var lostFile in resolver.ResolveResourceAsync())
                    totalLostFiles.Add(lostFile);
            }

            if (!totalLostFiles.Any())
                return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

            totalLostFiles.Shuffle();
            NeedToDownload = totalLostFiles.Count;

            var downloadList =
            (
                from f in totalLostFiles
                select new DownloadFile
                {
                    Completed = WhenCompleted,
                    DownloadPath = f.Path,
                    DownloadUri = f.Uri,
                    FileName = f.FileName,
                    FileSize = f.FileSize,
                    CheckSum = f.CheckSum,
                    FileType = f.Type,
                    TimeOut = 10000
                }).ToList();

            if (downloadList.First().FileType.Equals("GameJar", StringComparison.OrdinalIgnoreCase))
                downloadList.First().Changed = (_, args) =>
                {
                    DownloadFileCompletedEvent?.Invoke(this,
                        new DownloadFileCompletedEventArgs(null, null, downloadList.First(), args.Speed));
                };

            var (item1, item2) = await DownloadFiles(downloadList);

            return new TaskResult<ResourceCompleterCheckResult?>(item1, value: item2);
        }

        /// <summary>
        ///     IDisposable接口保留字段
        /// </summary>
        public void Dispose()
        {
        }

        void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
        {
            TotalDownloaded++;
            InvokeDownloadProgressChangedEvent((double)TotalDownloaded / NeedToDownload, e.AverageSpeed);
            DownloadFileCompletedEvent?.Invoke(this, e);

            if (!(e.Success ?? false))
            {
                _retryFiles.Add(e.File);
                return;
            }

            if (!CheckFile) return;

            Check(e.File, ref _retryFiles);
        }

        static void Check(DownloadFile file, ref ConcurrentBag<DownloadFile> bag)
        {
            var filePath = Path.Combine(file.DownloadPath, file.FileName);
            if (!File.Exists(filePath)) return;

#pragma warning disable CA5350 // 不要使用弱加密算法
            using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

            try
            {
                var hash = CryptoHelper.ComputeFileHash(filePath, hA);

                if (string.IsNullOrEmpty(file.CheckSum)) return;
                if (hash.Equals(file.CheckSum, StringComparison.OrdinalIgnoreCase)) return;

                bag.Add(file);
                File.Delete(filePath);
            }
            catch (Exception)
            {
            }
        }

        async Task<ValueTuple<TaskResultStatus, ResourceCompleterCheckResult?>> DownloadFiles(
            IEnumerable<DownloadFile> downloadList)
        {
            await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadParts);

            var leftRetries = TotalRetry;
            var fileBag = new ConcurrentBag<DownloadFile>(_retryFiles);

            while (!fileBag.IsEmpty && leftRetries >= 0)
            {
                _retryFiles.Clear();
                TotalDownloaded = 0;
                NeedToDownload = fileBag.Count;

                var files = fileBag.Select(f => (DownloadFile)f.Clone()).ToList();
                fileBag.Clear();

                foreach (var file in files)
                {
                    file.RetryCount++;
                    file.Completed = WhenCompleted;
                }

                await DownloadHelper.AdvancedDownloadListFile(files);

                fileBag = new ConcurrentBag<DownloadFile>(_retryFiles);
                leftRetries--;
            }

            var isLibraryFailed =
                fileBag.Any(f => f.FileType.Equals("Library/Native", StringComparison.OrdinalIgnoreCase));
            var resultType = fileBag.IsEmpty ? TaskResultStatus.Success : TaskResultStatus.PartialSuccess;
            if (isLibraryFailed) resultType = TaskResultStatus.Error;

            return (resultType, new ResourceCompleterCheckResult { IsLibDownloadFailed = isLibraryFailed });
        }

        void InvokeDownloadProgressChangedEvent(double progress, double speed)
        {
            DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
            {
                ProgressPercentage = progress,
                Speed = speed
            });
        }
    }
}