namespace ProjBobcat.Class.Model;

public readonly record struct GameLogEntry
{
    public GameLogType LogType { get; init; }
    public string? Time { get; init; }
    public string? Thread { get; init; }
    public string? Source { get; init; }
    public string? Content { get; init; }
    public string? ExceptionMsg { get; init; }
    public string? StackTrace { get; init; }
    public string? RawContent { get; init; }
}
