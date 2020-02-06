using System;

namespace ProjBobcat.Event
{
    public class GameLogEventArgs : EventArgs
    {
        public string LogType { get; set; }
        public string Content { get; set; }
    }
}