using System;
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
    /// 默认的资源补全器
    /// </summary>
    public class DefaultResourceCompleter : IResourceCompleter
    {
        private List<DownloadFile> _retryFileList = new List<DownloadFile>();
        private int _totalDownloaded, _needToDownload;
        private bool _needRetry;

        public int DownloadParts { get; set; } = 16;
        public int DownloadThread { get; set; }
        public int TotalRetry { get; set; }
        public bool CheckFile { get; set; }
        public IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }

        
        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;

        public bool CheckAndDownload()
        {
            return CheckAndDownloadTaskAsync().GetAwaiter().GetResult().Value;
        }

        public async Task<TaskResult<bool>> CheckAndDownloadTaskAsync()
        {
            if (!(ResourceInfoResolvers?.Any() ?? false))
                return new TaskResult<bool>(TaskResultStatus.Success, value: true);

            var totalLostFiles = new List<IGameResource>();
            foreach (var resolver in ResourceInfoResolvers)
            {
                resolver.GameResourceInfoResolveEvent += GameResourceInfoResolveStatus;
                var resolverResult = await resolver.ResolveResourceTaskAsync().ConfigureAwait(false);

                var gameResources = resolverResult.ToList();
                if (gameResources.Any())
                    totalLostFiles.AddRange(gameResources.ToList());
            }

            if (!totalLostFiles.Any()) return new TaskResult<bool>(TaskResultStatus.Success, value: true);

            DownloadFileCompletedEvent += (sender, args) =>
            {
                _totalDownloaded++;
                if (args.File.RetryCount != 0) _needToDownload++;

                InvokeDownloadProgressChangedEvent((double) _totalDownloaded / _needToDownload);
            };

            DownloadFileCompletedEvent += (sender, args) =>
            {
                if (CheckFile)
                {
                    if (!File.Exists(args.File.DownloadPath)) return;

#pragma warning disable CA5350 // 不要使用弱加密算法
                    using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

                    try
                    {
                        var hash = CryptoHelper.ComputeFileHash(args.File.DownloadPath, hA);

                        if (string.IsNullOrEmpty(args.File.CheckSum)) return;
                        if (hash.Equals(args.File.CheckSum, StringComparison.OrdinalIgnoreCase)) return;

                        File.Delete(args.File.DownloadPath);

                        if (TotalRetry == 0) return;
                        _retryFileList.Add(args.File);
                        _needRetry = true;
                        return;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (args.Success) return;
                if (TotalRetry == 0) return;
                _retryFileList.Add(args.File);
                _needRetry = true;
            };

            var downloadList = (from f in totalLostFiles
                    select new DownloadFile
                    {
                        Completed = DownloadFileCompletedEvent,
                        DownloadPath = f.Path,
                        DownloadUri = f.Uri,
                        FileName = f.Title,
                        FileSize = f.FileSize,
                        CheckSum = f.CheckSum,
                        FileType = f.Type
                    }
                ).OrderBy(x => x.FileSize).ToList();
            _needToDownload = downloadList.Count;

            var result = await DownloadFiles(downloadList).ConfigureAwait(true);

            return new TaskResult<bool>(result.Item1, value: result.Item2);
        }

        private async Task<Tuple<TaskResultStatus, bool>> DownloadFiles(IEnumerable<DownloadFile> downloadList)
        {
            return await Task.Run(async () =>
            {
                var retryCount = 0;

                await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadThread, null)
                        .ConfigureAwait(false);

                if (!_needRetry) return new Tuple<TaskResultStatus, bool>(TaskResultStatus.Success, false);

                while (_retryFileList.Any() && retryCount < TotalRetry)
                {
                    foreach (var rF in _retryFileList) rF.RetryCount++;

                    var tempList = _retryFileList;
                    await DownloadHelper.AdvancedDownloadListFile(tempList, DownloadThread, null, DownloadParts)
                        .ConfigureAwait(false);

                    retryCount++;
                }


                var flag = _retryFileList.Any(rF => rF.FileType.Equals("Library/Native", StringComparison.Ordinal));
                var resultType = _retryFileList.Any() ? TaskResultStatus.PartialSuccess : TaskResultStatus.Success;

                return new Tuple<TaskResultStatus, bool>(resultType, flag);
            }).ConfigureAwait(false);
        }

        private void InvokeDownloadProgressChangedEvent(double progress)
        {
            DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
            {
                ProgressPercentage = progress
            });
        }

        /// <summary>
        /// IDisposable接口保留字段
        /// </summary>
        public void Dispose(){}
    }
}