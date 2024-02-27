using System;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Event;

public class GameLogEventArgs : EventArgs
{
    public string? GameId { get; set; }

    public GameLogType LogType { get; set; }
    public string? Time { get; set; }
    public string? Source { get; set; }
    public string? RawContent { get; set; }
    public string? Content { get; set; }

    public string? ExceptionMsg { get; set; }
    public string? StackTrace { get; set; }
}