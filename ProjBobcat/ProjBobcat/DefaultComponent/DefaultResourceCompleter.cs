﻿using System;
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
        private bool _isLibraryFailed, _isNormalFileFailed;
        public int TotalDownloaded { get; set; }
        public int NeedToDownload { get; set; }

        public int DownloadParts { get; set; } = 16;
        public int TotalRetry { get; set; }
        public bool CheckFile { get; set; }
        public IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }


        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;

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
                if (!args.Success)
                {
                    var flag = args.File.FileType.Equals("Library/Native", StringComparison.Ordinal);
                    _isLibraryFailed = flag || _isLibraryFailed;
                    _isNormalFileFailed = !flag || _isLibraryFailed;
                }

                InvokeDownloadProgressChangedEvent((double) TotalDownloaded / NeedToDownload, args.AverageSpeed);
            };

            DownloadFileCompletedEvent += (_, args) =>
            {
                if (!CheckFile) return;

                var filePath = Path.Combine(args.File.DownloadPath, args.File.FileName);
                if (!File.Exists(filePath)) return;

#pragma warning disable CA5350 // 不要使用弱加密算法
                using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

                try
                {
                    var hash = CryptoHelper.ComputeFileHash(filePath, hA);

                    if (string.IsNullOrEmpty(args.File.CheckSum)) return;
                    if (hash.Equals(args.File.CheckSum, StringComparison.OrdinalIgnoreCase)) return;

                    File.Delete(filePath);

                    var flag = args.File.FileType.Equals("Library/Native", StringComparison.Ordinal);
                    _isLibraryFailed = flag || _isLibraryFailed;
                    _isNormalFileFailed = !flag || _isLibraryFailed;
                }
                catch (Exception)
                {
                }
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

        /// <summary>
        ///     IDisposable接口保留字段
        /// </summary>
        public void Dispose()
        {
        }

        private async Task<ValueTuple<TaskResultStatus, bool>> DownloadFiles(IEnumerable<DownloadFile> downloadList)
        {
            await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadParts);

            var resultType = _isNormalFileFailed ? TaskResultStatus.PartialSuccess : TaskResultStatus.Success;

            return (resultType, _isLibraryFailed);
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