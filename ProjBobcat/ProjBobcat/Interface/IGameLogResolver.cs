using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IGameLogResolver
{
    GameLogEntry Resolve(string rawLog)
    {
        var logType = ResolveLogType(rawLog);
        var totalPrefix = ResolveTotalPrefix(rawLog);

        return new GameLogEntry
        {
            LogType = logType,
            Time = ResolveTime(rawLog),
            Source = ResolveSource(rawLog),
            Content = string.IsNullOrEmpty(totalPrefix) ? rawLog : rawLog[totalPrefix.Length..],
            ExceptionMsg = logType == GameLogType.ExceptionMessage ? ResolveExceptionMsg(rawLog) : null,
            StackTrace = logType == GameLogType.StackTrace ? ResolveStackTrace(rawLog) : null,
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
