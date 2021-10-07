using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event
{
    public class GameResourceInfoResolveEventArgs : EventArgs
    {
        public double Progress { get; init; }
        public string Status { get; init; }
        public LogType LogType { get; init; }
    }
}