using System;

namespace ProjBobcat.Event
{
    public class DownloadFileChangedEventArgs : EventArgs
    {
        public double ProgressPercentage { get; set; }
        public long BytesReceived { get; set; }
        public long? TotalBytes { get; set; }
    }
}