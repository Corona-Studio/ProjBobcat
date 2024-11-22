using System;

namespace ProjBobcat.Event;

public class DownloadFileChangedEventArgs : EventArgs
{
    public double Speed { get; init; }
    public double ProgressPercentage { get; init; }
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
}