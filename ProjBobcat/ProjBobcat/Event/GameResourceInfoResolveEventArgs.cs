using System;

namespace ProjBobcat.Event;

public class GameResourceInfoResolveEventArgs : EventArgs
{
    public required double Progress { get; init; }
    public string? Status { get; init; }
}