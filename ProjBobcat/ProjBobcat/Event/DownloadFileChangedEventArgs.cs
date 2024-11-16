using System;

namespace ProjBobcat.Event;

public class DownloadFileChangedEventArgs : EventArgs
{
    /// <summary>
    ///     速度：字节 /秒
    /// </summary>
    public double Speed { get; set; }

    public double ProgressPercentage { get; set; }
    public long BytesReceived { get; set; }
    public long? TotalBytes { get; set; }
}