using System.Linq;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Logging
{
    public class DefaultGameLogResolver : IGameLogResolver
    {
        private const string LogTypeRegex = "FATAL|ERROR|WARN|INFO|DEBUG";
        private const string LogTimeRegex = "(20|21|22|23|[0-1]\\d):[0-5]\\d:[0-5]\\d";

        private string LogSourceAndTypeRegex => $"[a-zA-Z# _-]{{2,}}[0-9]?/({LogTypeRegex})";
        private string LogTotalPrefixRegex => $"\\[{LogTimeRegex}\\] \\[{LogSourceAndTypeRegex}\\]";

        private readonly Regex _sourceAndTypeRegex, _totalPrefixRegex, _typeRegex, _timeRegex;
        public DefaultGameLogResolver()
        {
            _typeRegex = new Regex(LogTypeRegex);
            _timeRegex = new Regex(LogTimeRegex);

            _sourceAndTypeRegex = new Regex(LogSourceAndTypeRegex);
            _totalPrefixRegex = new Regex(LogTotalPrefixRegex);
        }

        public GameLogType ResolveLogType(string log)
        {
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

        public string ResolveSource(string log)
        {
            return _sourceAndTypeRegex.Match(log).Value.Split('/').FirstOrDefault();
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