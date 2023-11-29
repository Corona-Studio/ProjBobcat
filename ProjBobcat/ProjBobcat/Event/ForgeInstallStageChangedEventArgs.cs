using System;

namespace ProjBobcat.Event;

public class ForgeInstallStageChangedEventArgs : EventArgs
{
    public required string CurrentStage { get; init; }
    public double Progress { get; init; }
}