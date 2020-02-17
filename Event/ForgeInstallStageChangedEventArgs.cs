using System;

namespace ProjBobcat.Event
{
    public class ForgeInstallStageChangedEventArgs : EventArgs
    {
        public string CurrentStage { get; set; }
        public double Progress { get; set; }
    }
}