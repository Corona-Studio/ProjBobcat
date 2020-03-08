using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IResourceCompleter
    {
        int DownloadThread { get; set; }
        int TotalRetry { get; set; }
        IEnumerable<IResourceInfoResolver> ResourceInfoResolvers { get; set; }
        bool CheckAndDownload();
        Task<TaskResult<bool>> CheckAndDownloadTaskAsync();

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;
        event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;
    }
}