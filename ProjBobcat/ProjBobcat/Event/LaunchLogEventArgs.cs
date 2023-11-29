using System;

namespace ProjBobcat.Event;

public class LaunchLogEventArgs : EventArgs
{
    public required string Item { get; init; }
    public required TimeSpan ItemRunTime { get; init; }
}