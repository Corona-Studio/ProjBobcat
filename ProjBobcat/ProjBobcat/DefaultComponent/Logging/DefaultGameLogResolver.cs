using System.Linq;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Logging
{
    public class DefaultGameLogResolver : IGameLogResolver
    {
        const string LogTypeRegex = "FATAL|ERROR|WARN|INFO|DEBUG";
        const string LogTimeRegex = "(20|21|22|23|[0-1]\\d):[0-5]\\d:[0-5]\\d";
        const string StackTraceAt = "(at .*)";
        const string ExceptionRegex = "(?m)^.*?Exception.*";

        readonly Regex
            _sourceAndTypeRegex,
            _totalPrefixRegex,
            _typeRegex,
            _timeRegex,
            _timeFullRegex,
            _stackTraceAtRegex,
            _exceptionRegex;

        public DefaultGameLogResolver()
        {
            _typeRegex = new Regex(LogTypeRegex);
            _timeRegex = new Regex(LogTimeRegex);
            _timeFullRegex = new Regex(LogDateRegex);

            _sourceAndTypeRegex = new Regex(LogSourceAndTypeRegex);
            _totalPrefixRegex = new Regex(LogTotalPrefixRegex);

            _stackTraceAtRegex = new Regex(StackTraceAt);
            _exceptionRegex = new Regex(ExceptionRegex);
        }

        string LogSourceAndTypeRegex => $"[\\w\\W\\s]{{2,}}/({LogTypeRegex})";
        string LogDateRegex => $"\\[{LogTimeRegex}\\]";
        string LogTotalPrefixRegex => $"\\[{LogTimeRegex}\\] \\[{LogSourceAndTypeRegex}\\]";

        public GameLogType ResolveLogType(string log)
        {
            if (!string.IsNullOrEmpty(ResolveExceptionMsg(log)))
                return GameLogType.ExceptionMessage;

            if (!string.IsNullOrEmpty(ResolveStackTrace(log)))
                return GameLogType.StackTrace;

            return _typeRegex.Match(log).Value switch
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
            var stackTrace = _stackTraceAtRegex.Match(log).Value;
            return stackTrace;
        }

        public string ResolveExceptionMsg(string log)
        {
            var exceptionMsg = _exceptionRegex.Match(log).Value;
            return exceptionMsg;
        }

        public string ResolveSource(string log)
        {
            var content = _sourceAndTypeRegex.Match(log).Value.Split('/').FirstOrDefault();
            var date = _timeFullRegex.Match(log).Value;
            var result = content?.Replace($"{date} [", string.Empty);

            return result;
        }

        public string ResolveTime(string log)
        {
            return _timeRegex.Match(log).Value;
        }

        public string ResolveTotalPrefix(string log)
        {
            return _totalPrefixRegex.Match(log).Value;
        }
    }
}