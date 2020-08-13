using System;

namespace ProjBobcat.Event
{
    public class DownloadFileChangedEventArgs : EventArgs
    {
        public double ProgressPercentage { get; set; }
    }
}