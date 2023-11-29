using System;

namespace ProjBobcat.Event;

public class StageChangedEventArgs : EventArgs
{
    public required string CurrentStage { get; init; }
    public required double Progress { get; init; }
}