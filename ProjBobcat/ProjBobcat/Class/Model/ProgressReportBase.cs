using System;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public class ProgressReportBase : IProgressReport
{
    public event EventHandler<StageChangedEventArgs>? StageChangedEventDelegate;

    protected void InvokeStatusChangedEvent(string currentStage, double progress)
    {
        this.StageChangedEventDelegate?.Invoke(this, new StageChangedEventArgs
        {
            CurrentStage = currentStage,
            Progress = progress
        });
    }
}