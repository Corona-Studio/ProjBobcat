using System;

namespace ProjBobcat.Event
{
    public class LaunchLogEventArgs : EventArgs
    {
        public string Item { get; set; }
        public TimeSpan ItemRunTime { get; set; }

        public LaunchLogEventArgs(string item, TimeSpan runTime)
        {
            Item = item;
            ItemRunTime = runTime;
        }
    }
}