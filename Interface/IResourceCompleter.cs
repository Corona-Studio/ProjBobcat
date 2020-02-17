using System;
using System.Net;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IResourceCompleter
    {
        string DownloadPath { get; set; }
        int DownloadThread { get; set; }
        event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;
        event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;
    }
}