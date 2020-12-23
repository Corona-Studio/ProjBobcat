using System;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model
{
    public abstract class InstallerBase : IInstaller
    {
        public string RootPath { get; set; }
        public string CustomId { get; set; }

        public event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;

        public virtual void InvokeStatusChangedEvent(string currentStage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new InstallerStageChangedEventArgs
            {
                CurrentStage = currentStage,
                Progress = progress
            });
        }
    }
}