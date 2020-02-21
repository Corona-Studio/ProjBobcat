using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using TaskResultStatus = ProjBobcat.Class.Model.TaskResultStatus;

namespace ProjBobcat.DefaultComponent
{
    public class DefaultResourceCompleter : IResourceCompleter
    {
        public int DownloadThread { get; set; }
        public int TotalRetry { get; set; }
        public IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;

        public bool CheckAndDownload()
        {
            throw new NotImplementedException();
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
                if (args.File.RetryCount != 0)
                {
                    _needToDownload++;
                }

                InvokeDownloadProgressChangedEvent((double)_totalDownloaded / _needToDownload);
            };

            var downloadList = (from f in totalLostFiles
                                select new DownloadFile
                                {
                                    Completed = DownloadFileCompletedEvent,
                                    DownloadPath = f.Path,
                                    DownloadUri = f.Uri,
                                    FileName = f.Title,
                                    FileSize = f.FileSize
                                }
                ).ToList();
            _needToDownload = downloadList.Count;

            var result = await DownloadFiles(downloadList).ConfigureAwait(true);

            return new TaskResult<bool>(result.Item1, value: result.Item2);
        }

        private int _totalDownloaded, _needToDownload;
        private async Task<Tuple<TaskResultStatus, bool>> DownloadFiles(IEnumerable<DownloadFile> downloadList)
        {
            var retryFileList = new List<DownloadFile>();
            var retryCount = 0;
            var needRetry = false;

            if (TotalRetry != 0)
            {
                DownloadFileCompletedEvent += (sender, args) =>
                {
                    if (args.Success) return;

                    retryFileList.Add(args.File);
                    needRetry = true;
                };
            }

            return await Task.Run(async () =>
            {
                if (retryCount == 0)
                {
                    await DownloadHelper.AdvancedDownloadListFile(downloadList, null).ConfigureAwait(false);
                }

                if (!needRetry) return new Tuple<TaskResultStatus, bool>(TaskResultStatus.Success, false);

                while (retryFileList.Any() && retryCount <= TotalRetry)
                {
                    foreach (var rF in retryFileList)
                    {
                        rF.RetryCount++;
                    }

                    var tempList = retryFileList;
                    retryFileList.Clear();
                    await DownloadHelper.AdvancedDownloadListFile(tempList, null).ConfigureAwait(false);

                    retryCount++;
                }


                var flag = retryFileList.Any(rF => rF.FileType.Equals("Library", StringComparison.Ordinal));
                var resultType = retryFileList.Any() ? TaskResultStatus.PartialSuccess : TaskResultStatus.Success;

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
    }
}