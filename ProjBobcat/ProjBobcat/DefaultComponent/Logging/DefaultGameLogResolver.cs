using System;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Logging;

/// <summary>
///     Resolves Minecraft game log lines produced by log4j / log4j2.
///     Supports vanilla (1.7+), Forge / NeoForge, Fabric, Quilt, legacy,
///     and plain level formats. Exception class lines, stack traces, and
///     chained-exception continuations are detected regardless of the
///     surrounding log format.
/// </summary>
public partial class DefaultGameLogResolver : IGameLogResolver
{
    public GameLogEntry Resolve(string rawLog)
    {
        if (string.IsNullOrEmpty(rawLog))
            return new GameLogEntry { LogType = GameLogType.Unknown, RawContent = rawLog };

        // log4j2 emits ANSI SGR escape sequences (\x1b[...m) for terminal coloring.
        // These leak through Process.StandardOutput and corrupt pattern matching + UI display.
        var log = AnsiEscapeRegex().Replace(rawLog, string.Empty);

        // Continuation lines first — they never carry their own prefix
        if (StackTraceRegex().IsMatch(log))
            return new GameLogEntry
            {
                LogType = GameLogType.StackTrace,
                StackTrace = log,
                RawContent = log
            };

        if (ExceptionClassRegex().IsMatch(log) || CausedByRegex().IsMatch(log))
            return new GameLogEntry
            {
                LogType = GameLogType.ExceptionMessage,
                ExceptionMsg = log,
                RawContent = log
            };

        // Forge / NeoForge log4j2 with locale-dependent date + millisecond time, optional [source]
        // [253月2026 23:06:46.358] [main/INFO] [cpw.mods.modlauncher.Launcher/MODLAUNCHER]: message
        // [25Mar2023 20:15:33.123] [Server thread/INFO] [net.minecraft/MinecraftServer]: message
        var match = ForgeTimestampRegex().Match(log);
        if (match.Success)
            return BuildEntry(match, log, useSource: true);

        // Fabric / Quilt log4j2 with parenthesized logger name
        // [14:32:01] [main/INFO] (FabricLoader) Loading 127 mods
        // [14:32:01] [Render thread/WARN] (minecraft): Missing texture
        match = FabricQuiltRegex().Match(log);
        if (match.Success)
            return BuildEntry(match, log, useSource: true);

        // Standard vanilla log4j2 (1.7+)
        // [20:15:33] [Server thread/INFO]: message
        match = VanillaRegex().Match(log);
        if (match.Success)
            return BuildEntry(match, log, useSource: false);

        // Legacy with ISO date (pre-1.7 / some server wrappers)
        // 2023-03-25 20:15:33 [INFO] [STDOUT] message
        match = LegacyDateRegex().Match(log);
        if (match.Success)
            return BuildEntry(match, log, useSource: false);

        // Bare level prefix (very old or custom launchers)
        // [INFO] message  or  [WARN]: message
        match = SimpleLevelRegex().Match(log);
        if (match.Success)
            return BuildEntry(match, log, useSource: false);

        return new GameLogEntry
        {
            LogType = GameLogType.Unknown,
            Content = log,
            RawContent = log
        };
    }

    #region Backward-compatible legacy methods

    public GameLogType ResolveLogType(string log) => Resolve(log).LogType;
    public string ResolveStackTrace(string log) => Resolve(log).StackTrace ?? string.Empty;
    public string ResolveExceptionMsg(string log) => Resolve(log).ExceptionMsg ?? string.Empty;
    public string ResolveSource(string log) => Resolve(log).Source ?? string.Empty;
    public string ResolveTime(string log) => Resolve(log).Time ?? string.Empty;

    public string ResolveTotalPrefix(string log)
    {
        var entry = Resolve(log);
        if (entry.Content == null || entry.RawContent == null) return string.Empty;

        var idx = entry.RawContent.IndexOf(entry.Content, StringComparison.Ordinal);
        return idx > 0 ? entry.RawContent[..idx] : string.Empty;
    }

    #endregion

    static GameLogEntry BuildEntry(Match match, string rawLog, bool useSource)
    {
        var timeGroup = match.Groups["time"];
        var threadGroup = match.Groups["thread"];
        var sourceGroup = match.Groups["source"];
        var contentGroup = match.Groups["content"];

        var thread = threadGroup is { Success: true, Length: > 0 } ? threadGroup.Value : null;
        var source = useSource && sourceGroup is { Success: true, Length: > 0 }
            ? sourceGroup.Value
            : thread;

        return new GameLogEntry
        {
            LogType = ParseLogLevel(match.Groups["level"].Value),
            Time = timeGroup is { Success: true, Length: > 0 } ? timeGroup.Value : null,
            Thread = thread,
            Source = source,
            Content = contentGroup is { Success: true, Length: > 0 } ? contentGroup.Value : null,
            RawContent = rawLog
        };
    }

    static GameLogType ParseLogLevel(string level) => level.ToUpperInvariant() switch
    {
        "FATAL" => GameLogType.Fatal,
        "ERROR" => GameLogType.Error,
        "WARN" or "WARNING" => GameLogType.Warning,
        "INFO" => GameLogType.Info,
        "DEBUG" => GameLogType.Debug,
        "TRACE" => GameLogType.Debug,
        _ => GameLogType.Unknown
    };

    #region Source-generated regex patterns

    // ANSI SGR escape sequences: ESC[ (params) m — emitted by log4j2 for terminal coloring
    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex AnsiEscapeRegex();

    // Forge / NeoForge log4j2 — date + time with milliseconds, thread/level, optional [source].
    // The date portion uses locale-dependent month names via log4j2 %d{ddMMMyyyy ...},
    // e.g. "25Mar2026" (English), "253月2026" (Chinese/Japanese), "25mars2026" (French).
    // We skip the locale-dependent date prefix and only capture HH:mm:ss.SSS for the time field.
    [GeneratedRegex(
        @"^\[[^\]]*(?<time>\d{2}:\d{2}:\d{2}\.\d{3})\] \[(?<thread>[^/\]]+)/(?<level>FATAL|ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE)\](?: \[(?<source>[^\]]*)\])?:?\s?(?<content>.*)")]
    private static partial Regex ForgeTimestampRegex();

    // Fabric / Quilt log4j2 — time, thread/level, parenthesized logger name
    [GeneratedRegex(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\] \[(?<thread>[^/\]]+)/(?<level>FATAL|ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE)\] \((?<source>[^)]+)\):?\s?(?<content>.*)")]
    private static partial Regex FabricQuiltRegex();

    // Vanilla Minecraft log4j2 (1.7+) — time-only, thread/level
    [GeneratedRegex(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\] \[(?<thread>[^/\]]+)/(?<level>FATAL|ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE)\]:?\s?(?<content>.*)")]
    private static partial Regex VanillaRegex();

    // Legacy with ISO date — optional bracketed source
    [GeneratedRegex(
        @"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(?<level>FATAL|ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE)\](?: \[(?<thread>[^\]]*)\])?\s?(?<content>.*)")]
    private static partial Regex LegacyDateRegex();

    // Bare [LEVEL] prefix
    [GeneratedRegex(
        @"^\[(?<level>FATAL|ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE)\]:?\s?(?<content>.*)")]
    private static partial Regex SimpleLevelRegex();

    // Java stack trace continuation: "    at pkg.Class.method(File.java:123)" or "    ... 5 more"
    [GeneratedRegex(@"^\s+at\s|^\s+\.\.\.\s\d+\smore")]
    private static partial Regex StackTraceRegex();

    // Fully-qualified Java exception class: "java.lang.NullPointerException: msg"
    [GeneratedRegex(@"^[\w.$]+(?:Exception|Error|Throwable)\b")]
    private static partial Regex ExceptionClassRegex();

    // Chained / suppressed exception continuation
    [GeneratedRegex(@"^(?:Caused by|Suppressed):\s")]
    private static partial Regex CausedByRegex();

    #endregion
}
