using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event;

public class ForgeInstallStageChangedEventArgs : EventArgs
{
    public required string CurrentStage { get; init; }
    public ProgressValue Progress { get; init; }
}