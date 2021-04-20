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
    /// <summary>
    ///     默认的资源补全器
    /// </summary>
    public class DefaultResourceCompleter : IResourceCompleter
    {
        private ConcurrentBag<DownloadFile> _retryFiles;
        public int TotalDownloaded { get; set; }
        public int NeedToDownload { get; set; }

        public int DownloadParts { get; set; } = 16;
        public int TotalRetry { get; set; }
        public bool CheckFile { get; set; }
        public IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }


        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;

        public DefaultResourceCompleter()
        {
            _retryFiles = new ConcurrentBag<DownloadFile>();
        }

        public bool CheckAndDownload()
        {
            return CheckAndDownloadTaskAsync().Result.Value;
        }

        public async Task<TaskResult<bool>> CheckAndDownloadTaskAsync()
        {
            if (!(ResourceInfoResolvers?.Any() ?? false))
                return new TaskResult<bool>(TaskResultStatus.Success, value: true);

            var totalLostFiles = new List<IGameResource>();
            foreach (var resolver in ResourceInfoResolvers)
            {
                resolver.GameResourceInfoResolveEvent += GameResourceInfoResolveStatus;

                await foreach (var lostFile in resolver.ResolveResourceAsync())
                    totalLostFiles.Add(lostFile);
            }

            if (!totalLostFiles.Any()) return new TaskResult<bool>(TaskResultStatus.Success, value: true);

            DownloadFileCompletedEvent += (_, args) =>
            {
                TotalDownloaded++;
                InvokeDownloadProgressChangedEvent((double)TotalDownloaded / NeedToDownload, args.AverageSpeed);

                if (!args.Success)
                {
                    _retryFiles.Add(args.File);
                    return;
                }

                if (!CheckFile) return;

                Check(args.File, ref _retryFiles);
            };

            var downloadList = (from f in totalLostFiles
                    select new DownloadFile
                    {
                        Completed = DownloadFileCompletedEvent,
                        DownloadPath = f.Path,
                        DownloadUri = f.Uri,
                        FileName = f.FileName,
                        FileSize = f.FileSize,
                        CheckSum = f.CheckSum,
                        FileType = f.Type
                    }
                ).ToList();

            downloadList.Shuffle();

            NeedToDownload = downloadList.Count;

            var (item1, item2) = await DownloadFiles(downloadList);

            return new TaskResult<bool>(item1, value: item2);
        }

        private static void Check(DownloadFile file, ref ConcurrentBag<DownloadFile> bag)
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
            catch (Exception) { }
        }

        /// <summary>
        ///     IDisposable接口保留字段
        /// </summary>
        public void Dispose()
        {
        }

        private async Task<ValueTuple<TaskResultStatus, bool>> DownloadFiles(IEnumerable<DownloadFile> downloadList)
        {
            await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadParts);

            var leftRetries = TotalRetry;
            var fileBag = new ConcurrentBag<DownloadFile>(_retryFiles);

            TotalDownloaded = 0;
            NeedToDownload = fileBag.Count;

            while (!fileBag.IsEmpty && leftRetries != 0)
            {
                var taken = fileBag.TryTake(out var outFile);
                if(!taken) continue;

                var file = (DownloadFile) outFile.Clone();
                file.Completed = (_, args) =>
                {
                    TotalDownloaded++;
                    InvokeDownloadProgressChangedEvent((double)TotalDownloaded / NeedToDownload, args.AverageSpeed);

                    if (!args.Success)
                    {
                        fileBag.Add(args.File);
                        return;
                    }

                    if (!CheckFile) return;

                    Check(args.File, ref fileBag);
                };

                await DownloadHelper.DownloadData(file);
                leftRetries--;
            }

            var isLibraryFailed = fileBag.Any(f => f.FileType.Equals("Library/Native", StringComparison.Ordinal));

            var resultType = fileBag.IsEmpty ? TaskResultStatus.Success : TaskResultStatus.PartialSuccess;
            if (isLibraryFailed)
            {
                resultType = TaskResultStatus.Error;
            }

            return (resultType, isLibraryFailed);
        }

        private void InvokeDownloadProgressChangedEvent(double progress, double speed)
        {
            DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
            {
                ProgressPercentage = progress,
                Speed = speed
            });
        }
    }
}