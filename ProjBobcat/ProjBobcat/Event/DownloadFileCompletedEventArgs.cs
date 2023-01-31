using System;

namespace ProjBobcat.Event;

public class DownloadFileCompletedEventArgs : EventArgs
{
    public DownloadFileCompletedEventArgs(bool? success, Exception? ex, double averageSpeed)
    {
        Success = success;
        Error = ex;
        AverageSpeed = averageSpeed;
    }

    public double AverageSpeed { get; set; }
    public bool? Success { get; }
    public Exception? Error { get; }
}