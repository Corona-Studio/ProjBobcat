using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event
{
    public class DownloadFileCompletedEventArgs
    {
        public DownloadFileCompletedEventArgs(bool success, Exception ex, DownloadFile file)
        {
            Success = success;
            Error = ex;
            File = file;
        }

        public DownloadFile File { get; }
        public bool Success { get; }
        public Exception Error { get; }
    }
}