using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IGameLogResolver
{
    GameLogEntry Resolve(string rawLog)
    {
        var logType = this.ResolveLogType(rawLog);
        var totalPrefix = this.ResolveTotalPrefix(rawLog);

        return new GameLogEntry
        {
            LogType = logType,
            Time = this.ResolveTime(rawLog),
            Source = this.ResolveSource(rawLog),
            Content = string.IsNullOrEmpty(totalPrefix) ? rawLog : rawLog[totalPrefix.Length..],
            ExceptionMsg = logType == GameLogType.ExceptionMessage ? this.ResolveExceptionMsg(rawLog) : null,
            StackTrace = logType == GameLogType.StackTrace ? this.ResolveStackTrace(rawLog) : null,
            RawContent = rawLog
        };
    }

    GameLogType ResolveLogType(string log);
    string ResolveSource(string log);
    string ResolveTime(string log);
    string ResolveTotalPrefix(string log);

    string ResolveStackTrace(string log);
    string ResolveExceptionMsg(string log);
}