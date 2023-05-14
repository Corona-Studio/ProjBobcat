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

#if NET7_0_OR_GREATER
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

#else

    static readonly Regex
        SourceAndTypeRegex = new(LogSourceAndTypeRegex, RegexOptions.Compiled),
        TotalPrefixRegex = new(LogTotalPrefixRegex, RegexOptions.Compiled),
        TypeRegex = new(LogTypeRegexStr, RegexOptions.Compiled),
        TimeRegex = new(LogTimeRegexStr, RegexOptions.Compiled),
        TimeFullRegex = new(LogDateRegex, RegexOptions.Compiled),
        StackTraceAtRegex = new(StackTraceAtStr, RegexOptions.Compiled),
        ExceptionRegex = new(ExceptionRegexStr, RegexOptions.Compiled);

#endif

    public GameLogType ResolveLogType(string log)
    {
        if (!string.IsNullOrEmpty(ResolveExceptionMsg(log)))
            return GameLogType.ExceptionMessage;

        if (!string.IsNullOrEmpty(ResolveStackTrace(log)))
            return GameLogType.StackTrace;

#if NET7_0_OR_GREATER
        return TypeRegex().Match(log).Value switch
        {
            "FATAL" => GameLogType.Fatal,
            "ERROR" => GameLogType.Error,
            "WARN" => GameLogType.Warning,
            "INFO" => GameLogType.Info,
            "DEBUG" => GameLogType.Debug,
            _ => GameLogType.Unknown
        };
#else
        return TypeRegex.Match(log).Value switch
        {
            "FATAL" => GameLogType.Fatal,
            "ERROR" => GameLogType.Error,
            "WARN" => GameLogType.Warning,
            "INFO" => GameLogType.Info,
            "DEBUG" => GameLogType.Debug,
            _ => GameLogType.Unknown
        };
#endif
    }

    public string ResolveStackTrace(string log)
    {
#if NET7_0_OR_GREATER
        var stackTrace = StackTraceAtRegex().Match(log).Value;
#else
        var stackTrace = StackTraceAtRegex.Match(log).Value;
#endif

        return stackTrace;
    }

    public string ResolveExceptionMsg(string log)
    {
#if NET7_0_OR_GREATER
        var exceptionMsg = ExceptionRegex().Match(log).Value;
#else
        var exceptionMsg = ExceptionRegex.Match(log).Value;
#endif

        return exceptionMsg;
    }

    public string ResolveSource(string log)
    {
#if NET7_0_OR_GREATER
        var content = SourceAndTypeRegex().Match(log).Value.Split('/').FirstOrDefault();
        var date = TimeFullRegex().Match(log).Value;
#else
        var content = SourceAndTypeRegex.Match(log).Value.Split('/').FirstOrDefault();
        var date = TimeFullRegex.Match(log).Value;
#endif

        var result = content?.Replace($"{date} [", string.Empty);

        return result;
    }

    public string ResolveTime(string log)
    {
#if NET7_0_OR_GREATER
        return TimeRegex().Match(log).Value;
#else
        return TimeRegex.Match(log).Value;
#endif
    }

    public string ResolveTotalPrefix(string log)
    {
#if NET7_0_OR_GREATER
        return TotalPrefixRegex().Match(log).Value;
#else
        return TotalPrefixRegex.Match(log).Value;
#endif
    }
}