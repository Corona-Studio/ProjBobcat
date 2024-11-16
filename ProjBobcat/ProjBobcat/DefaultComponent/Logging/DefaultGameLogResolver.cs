using System.Linq;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Logging;

public partial class DefaultGameLogResolver : IGameLogResolver
{
    const string LogTypeRegexStr = "FATAL|ERROR|WARN|INFO|DEBUG";
    const string LogTimeRegexStr = "(20|21|22|23|[0-1]\\d):[0-5]\\d:[0-5]\\d";
    const string StackTraceAtStr = "(at .*)";
    const string ExceptionRegexStr = "(?m)^.*?Exception.*";

    const string LogSourceAndTypeRegex = $"[\\w\\W\\s]{{2,}}/({LogTypeRegexStr})";
    const string LogDateRegex = $"\\[{LogTimeRegexStr}\\]";
    const string LogTotalPrefixRegex = $"\\[{LogTimeRegexStr}\\] \\[{LogSourceAndTypeRegex}\\]";

    public GameLogType ResolveLogType(string log)
    {
        if (!string.IsNullOrEmpty(this.ResolveExceptionMsg(log)))
            return GameLogType.ExceptionMessage;

        if (!string.IsNullOrEmpty(this.ResolveStackTrace(log)))
            return GameLogType.StackTrace;

        return TypeRegex().Match(log).Value switch
        {
            "FATAL" => GameLogType.Fatal,
            "ERROR" => GameLogType.Error,
            "WARN" => GameLogType.Warning,
            "INFO" => GameLogType.Info,
            "DEBUG" => GameLogType.Debug,
            _ => GameLogType.Unknown
        };
    }

    public string ResolveStackTrace(string log)
    {
        var stackTrace = StackTraceAtRegex().Match(log).Value;

        return stackTrace;
    }

    public string ResolveExceptionMsg(string log)
    {
        var exceptionMsg = ExceptionRegex().Match(log).Value;

        return exceptionMsg;
    }

    public string ResolveSource(string log)
    {
        var content = SourceAndTypeRegex().Match(log).Value.Split('/').FirstOrDefault();

        if (string.IsNullOrEmpty(content)) return string.Empty;

        var date = TimeFullRegex().Match(log).Value;
        var result = content.Replace($"{date} [", string.Empty);

        return result;
    }

    public string ResolveTime(string log)
    {
        return TimeRegex().Match(log).Value;
    }

    public string ResolveTotalPrefix(string log)
    {
        return TotalPrefixRegex().Match(log).Value;
    }

    [GeneratedRegex(LogSourceAndTypeRegex)]
    private static partial Regex SourceAndTypeRegex();

    [GeneratedRegex(LogTotalPrefixRegex)]
    private static partial Regex TotalPrefixRegex();

    [GeneratedRegex(LogTypeRegexStr)]
    private static partial Regex TypeRegex();

    [GeneratedRegex(LogTimeRegexStr)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(LogDateRegex)]
    private static partial Regex TimeFullRegex();

    [GeneratedRegex(StackTraceAtStr)]
    private static partial Regex StackTraceAtRegex();

    [GeneratedRegex(ExceptionRegexStr)]
    private static partial Regex ExceptionRegex();
}