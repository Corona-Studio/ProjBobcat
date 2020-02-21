using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event
{
    public class GameResourceInfoResolveEventArgs : EventArgs
    {
        public string CurrentProgress { get; set; }
        public LogType LogType { get; set; }
    }
}