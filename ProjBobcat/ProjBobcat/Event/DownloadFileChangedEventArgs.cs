using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event;

public class DownloadFileChangedEventArgs : EventArgs
{
    public double Speed { get; init; }
    public ProgressValue ProgressPercentage { get; init; }
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
}