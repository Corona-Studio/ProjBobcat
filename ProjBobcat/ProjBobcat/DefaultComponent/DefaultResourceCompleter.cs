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
    ///     默认的资源补全器
    /// </summary>
    public class DefaultResourceCompleter : IResourceCompleter
    {
        private bool _isLibraryFailed, _isNormalFileFailed;
        private int _totalDownloaded, _needToDownload;

        public int DownloadParts { get; set; } = 16;
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
                    totalLostFiles.AddRange(gameResources);
            }

            if (!totalLostFiles.Any()) return new TaskResult<bool>(TaskResultStatus.Success, value: true);

            DownloadFileCompletedEvent += (sender, args) =>
            {
                _totalDownloaded++;

                InvokeDownloadProgressChangedEvent((double) _totalDownloaded / _needToDownload);
            };

            DownloadFileCompletedEvent += (sender, args) =>
            {
                if (!CheckFile) return;
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
                ).OrderByDescending(x => x.FileSize).ToList();
            _needToDownload = downloadList.Count;

            var result = await DownloadFiles(downloadList).ConfigureAwait(true);

            return new TaskResult<bool>(result.Item1, value: result.Item2);
        }

        /// <summary>
        ///     IDisposable接口保留字段
        /// </summary>
        public void Dispose()
        {
        }

        private async Task<Tuple<TaskResultStatus, bool>> DownloadFiles(IEnumerable<DownloadFile> downloadList)
        {
            await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadParts)
                .ConfigureAwait(false);

            var resultType = _isNormalFileFailed ? TaskResultStatus.PartialSuccess : TaskResultStatus.Success;

            return new Tuple<TaskResultStatus, bool>(resultType, _isLibraryFailed);
        }

        private void InvokeDownloadProgressChangedEvent(double progress)
        {
            DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
            {
                ProgressPercentage = progress
            });
        }
    }
}