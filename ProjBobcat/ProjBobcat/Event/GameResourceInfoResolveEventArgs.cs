using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event;

public class GameResourceInfoResolveEventArgs : EventArgs
{
    public required ProgressValue Progress { get; init; }
    public string? Status { get; init; }
}