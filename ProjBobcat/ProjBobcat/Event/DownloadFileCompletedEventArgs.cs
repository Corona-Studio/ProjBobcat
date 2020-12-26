using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event
{
    public class DownloadFileCompletedEventArgs : EventArgs
    {
        public DownloadFileCompletedEventArgs(bool success, Exception ex, DownloadFile file, double averageSpeed)
        {
            Success = success;
            Error = ex;
            File = file;
            AverageSpeed = averageSpeed;
        }

        public double AverageSpeed { get; set; }
        public DownloadFile File { get; }
        public bool Success { get; }
        public Exception Error { get; }
    }
}