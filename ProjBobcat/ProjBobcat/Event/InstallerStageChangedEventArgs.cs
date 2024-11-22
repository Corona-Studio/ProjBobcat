using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event;

public class StageChangedEventArgs : EventArgs
{
    public required string CurrentStage { get; init; }
    public required ProgressValue Progress { get; init; }
}