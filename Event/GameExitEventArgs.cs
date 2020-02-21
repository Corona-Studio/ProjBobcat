using System;

namespace ProjBobcat.Event
{
    public class GameExitEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public int ExitCode { get; set; }
    }
}