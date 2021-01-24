using System;

namespace ProjBobcat.Event
{
    public class StageChangedEventArgs : EventArgs
    {
        public string CurrentStage { get; set; }
        public double Progress { get; set; }
    }
}