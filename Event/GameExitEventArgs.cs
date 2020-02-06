using System;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Event
{
    public class GameExitEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public int ExitCode { get; set; }

        public GameExitEventArgs(Exception ex, int exitCode)
        {
            Exception = ex;
            ExitCode = exitCode;
        }
    }
}