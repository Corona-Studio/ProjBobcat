using System;

namespace ProjBobcat.Event
{
    public class InstallerStageChangedEventArgs : EventArgs
    {
        public string CurrentStage { get; set; }
        public double Progress { get; set; }
    }
}