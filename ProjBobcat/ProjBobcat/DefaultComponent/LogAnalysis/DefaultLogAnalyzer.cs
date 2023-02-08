using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Helper;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.LogAnalysis;

public class DefaultLogAnalyzer : ILogAnalyzer
{
    public string? RootPath { get; init; }
    public string? GameId { get; init; }
    public bool VersionIsolation { get; init; }
    public IReadOnlyList<string>? CustomLogFiles { get; init; }
    public double LogFileLastWriteTimeLimit { get; init; } = 10;

    static LogFileType GetLogFileType(FileInfo fi)
    {
        var fileName = Path.GetFileName(fi.FullName);

        if (fileName.Contains("hs_err")) return LogFileType.HsError;
        if (fileName.Contains("crash-")) return LogFileType.CrashReport;
        if (fileName == "latest.log") return LogFileType.GameLog;

        var ext = Path.GetExtension(fileName);

        if (ext is ".log" or ".txt") return LogFileType.ExtraLog;

        return LogFileType.Unknown;
    }

    static bool IsValidLogFile(FileInfo fi, double minutesAgo = 3)
    {
        if (!fi.Exists) return false;
        if (fi.Length == 0) return false;
        if((DateTime.Now - fi.LastWriteTime).TotalMinutes > minutesAgo) return false;

        return true;
    }

    IEnumerable<(LogFileType, (string, string))> GetAllLogs()
    {
        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("未提供 RootPath 参数");
        if (string.IsNullOrEmpty(GameId))
            throw new NullReferenceException("未提供 GameId 参数");

        var logFiles = new List<FileInfo>();
        var fullRootPath = Path.GetFullPath(RootPath);
        var versionPath = Path.Combine(fullRootPath, GamePathHelper.GetGamePath(GameId));

        var crashReportDi = new DirectoryInfo(Path.Combine(versionPath, "crash-reports"));
        if(crashReportDi.Exists)
            logFiles.AddRange(crashReportDi.GetFiles().Where(fi => fi.Extension is ".log" or ".txt"));

        var versionDi = new DirectoryInfo(VersionIsolation ? RootPath : versionPath);
        if(versionDi.Exists)
            logFiles.AddRange(versionDi.GetFiles().Where(fi => fi.Extension == ".log"));

        logFiles.Add(new FileInfo(Path.Combine(versionPath, "logs", "latest.log")));
        logFiles.Add(new FileInfo(Path.Combine(versionPath, "logs", "debug.log")));

        if(CustomLogFiles?.Any() ?? false)
            logFiles.AddRange(CustomLogFiles.Select(custom => new FileInfo(custom)));

        foreach (var log in logFiles)
        {
            if (!IsValidLogFile(log, LogFileLastWriteTimeLimit)) continue;

            var logType = GetLogFileType(log);

            if (logType == LogFileType.Unknown) continue;

            var lines = File.ReadAllText(log.FullName);

            if(!lines.Any()) continue;

            yield return (logType, (log.FullName, lines));
        }
    }

    #region Game Log Analyze

    static readonly Dictionary<string, CrashCauses> GameLogsCausesMap = new ()
    {
        { "Found multiple arguments for option fml.forgeVersion, but you asked for only one", CrashCauses.MultipleForgeInVersionJson },
        { "The driver does not appear to support OpenGL", CrashCauses.GpuDoesNotSupportOpenGl },
        { "java.lang.ClassCastException: java.base/jdk", CrashCauses.JdkUse },
        { "java.lang.ClassCastException: class jdk.", CrashCauses.JdkUse },
        { "TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java", CrashCauses.IncompatibleForgeAndOptifine },
        { "Open J9 is not supported", CrashCauses.OpenJ9Use },
        { "OpenJ9 is incompatible", CrashCauses.OpenJ9Use },
        { ".J9VMInternals.", CrashCauses.OpenJ9Use },
        { "java.lang.NoSuchFieldException: ucp", CrashCauses.JavaVersionTooHigh },
        { "because module java.base does not export", CrashCauses.JavaVersionTooHigh },
        { "java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory", CrashCauses.JavaVersionTooHigh },
        { "java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory", CrashCauses.JavaVersionTooHigh },
        { "The directories below appear to be extracted jar files. Fix this before you continue.", CrashCauses.DecompressedMod },
        { "Extracted mod jars found, loading will NOT continue", CrashCauses.DecompressedMod },
        { "Couldn't set pixel format", CrashCauses.UnableToSetPixelFormat },
        { "java.lang.OutOfMemoryError", CrashCauses.NoEnoughMemory },
        { "java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier", CrashCauses.LegacyForgeDoesNotSupportNewerJava },
        { "1282: Invalid operation", CrashCauses.OpenGl1282Error },
        { "Maybe try a lower resolution resourcepack?", CrashCauses.TextureTooLargeOrLowEndGpu },
        { "Unsupported class file major version", CrashCauses.UnsupportedJavaVersion },
        { "Caught exception from ", CrashCauses.ModCausedGameCrash },
        { "java.lang.UnsupportedClassVersionError: net/fabricmc/loader/impl/launch/knot/KnotClient : Unsupported major.minor version", CrashCauses.UnsupportedJavaVersion },
        {
            "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>(int[], int, net.minecraft.core.Direction, net.minecraft.client.renderer.texture.TextureAtlasSprite, boolean, boolean)'",
            CrashCauses.IncompatibleForgeAndOptifine
        },
        {
            "java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager.addRegionTicket(net.minecraft.server.level.TicketType, net.minecraft.world.level.ChunkPos, int, java.lang.Object, boolean)'",
            CrashCauses.IncompatibleForgeAndOptifine
        }
    };

    static readonly Regex PackSignerMatch =
        new ("(?<=class \")[^']+(?=\"'s signer information)", RegexOptions.Compiled);

    static readonly Regex ForgeErrorMatch =
        new(@"(?<=the game will display an error screen and halt[\s\S]+?Exception: )[\s\S]+?(?=\n\tat)",
            RegexOptions.Compiled);

    static readonly Regex FabricSolutionMatch =
        new(@"(?<=A potential solution has been determined:\n)((\t)+ - [^\n]+\n)+", RegexOptions.Compiled);

    static readonly Regex GameModMatch1 =
        new(@"(?<=\n\t[\w]+ : [A-Z]{1}:[^\n]+(/|\\))[^/\\\n]+?.jar", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex GameModMatch2 =
        new(@"Found a duplicate mod[^\n]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex GameModMatch3 = new(@"ModResolutionException: Duplicate[^\n]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex ModIdMatch1 = new("(?<=in )[^./ ]+(?=.mixins.json.+failed injection check)");

    static readonly Regex ModIdMatch2 = new("(?<= failed .+ in )[^./ ]+(?=.mixins.json)");

    static readonly Regex ModIdMatch3 = new(@"(?<= in config \[)[^./ ]+(?=.mixins.json\] FAILED during )");

    static readonly Regex ModIdMatch4 = new("(?<= in callback )[^./ ]+(?=.mixins.json:)");

    static readonly Regex MainClassMatch1 = new(@"^[^\n.]+.\w+.[^\n]+\n\[$", RegexOptions.Compiled);

    static readonly Regex MainClassMatch2 = new(@"^\[[^\]]+\] [^\n.]+.\w+.[^\n]+\n\[", RegexOptions.Compiled);

    static IEnumerable<AnalysisReport.AnalysisReport> ProcessGameLogs(string logs)
    {
        foreach (var causeMap in GameLogsCausesMap)
        {
            var (matchStr, cause) = causeMap;

            if (logs.Contains(matchStr))
                yield return new AnalysisReport.AnalysisReport(cause);
        }

        if (logs.Contains("signer information does not match signer information of other classes in the same package"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.ContentValidationFailed)
            {
                Details = new[] { PackSignerMatch.Match(logs).Value }
            };

        if (logs.Contains("An exception was thrown, the game will display an error screen and halt."))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.ForgeError)
            {
                Details = new[] { ForgeErrorMatch.Match(logs).Value }
            };

        if (logs.Contains("A potential solution has been determined:"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.FabricErrorWithSolution)
            {
                Details = new[] { FabricSolutionMatch.Match(logs).Value }
            };

        if (logs.Contains("java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") &&
            logs.Contains("OptiFine"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.FailedToLoadWorldBecauseOptiFine);
        
        if (logs.Contains("Could not reserve enough space"))
        {
            if (logs.Contains("for 1048576KB object heap"))
                yield return new AnalysisReport.AnalysisReport(CrashCauses.NoEnoughMemory32);
            else
                yield return new AnalysisReport.AnalysisReport(CrashCauses.NoEnoughMemory);
        }

        // Mod 重复安装
        if (logs.Contains("DuplicateModsFoundException"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.DuplicateMod)
            {
                Details = new[] { GameModMatch1.Match(logs).Value }
            };


        if (logs.Contains("Found a duplicate mod"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.DuplicateMod)
            {
                Details = new[] { GameModMatch2.Match(logs).Value }
            };
        
        if (logs.Contains("ModResolutionException: Duplicate"))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.DuplicateMod)
            {
                Details = new[] { GameModMatch3.Match(logs).Value }
            };

        // Mod 导致的崩溃
        if (logs.Contains("Mixin prepare failed ")
            || logs.Contains("Mixin apply failed ")
            || logs.Contains("mixin.injection.throwables.")
            || logs.Contains(".mixins.json] FAILED during )"))
        {
            var modId = ModIdMatch1.Match(logs).Value;

            if(string.IsNullOrEmpty(modId))
                modId = ModIdMatch2.Match(logs).Value;
            if(string.IsNullOrEmpty(modId))
                modId = ModIdMatch3.Match(logs).Value;
            if(string.IsNullOrEmpty(modId))
                modId = ModIdMatch4.Match(logs).Value;

            yield return new AnalysisReport.AnalysisReport(CrashCauses.ModMixinFailed)
            {
                Details = string.IsNullOrEmpty(modId) ? null : new[] { modId }
            };
        }
    }

    #endregion

    #region Hs Log Analyze

    static readonly Dictionary<string, CrashCauses> HsCausesMap = new ()
    {
        { "The system is out of physical RAM or swap space", CrashCauses.NoEnoughMemory },
        { "Out of Memory Error", CrashCauses.NoEnoughMemory }
    };

    static IEnumerable<AnalysisReport.AnalysisReport> ProcessHsLogs(string logs)
    {
        foreach (var causeMap in HsCausesMap)
        {
            var (matchStr, cause) = causeMap;

            if (logs.Contains(matchStr))
                yield return new AnalysisReport.AnalysisReport(cause);
        }

        if (logs.Contains("EXCEPTION_ACCESS_VIOLATION"))
        {
            if (logs.Contains("# C  [ig"))
                yield return new AnalysisReport.AnalysisReport(CrashCauses.UnsupportedIntelDriver);
            if (logs.Contains("# C  [atio"))
                yield return new AnalysisReport.AnalysisReport(CrashCauses.UnsupportedAmdDriver);
            if (logs.Contains("# C  [nvoglv"))
                yield return new AnalysisReport.AnalysisReport(CrashCauses.UnsupportedNvDriver);
        }
    }

    #endregion

    #region Crash Report Analyze

    static readonly Dictionary<string, CrashCauses> CrashCausesMap = new()
    {
        { "maximum id range exceeded", CrashCauses.ModIdExceeded },
        { "java.lang.OutOfMemoryError", CrashCauses.NoEnoughMemory },
        { "Pixel format not accelerated", CrashCauses.UnableToSetPixelFormat },
        { "Manually triggered debug crash", CrashCauses.ManuallyTriggeredDebugCrash }
    };

    static readonly Regex ModFileMatch = new("(?<=Mod File: ).+", RegexOptions.Compiled);
    static readonly Regex ModLoaderMatch = new(@"(?<=Failure message: )[\w\W]+?(?=\tMod)", RegexOptions.Compiled);

    static readonly Regex MultipleEntriesMatch =
        new("(?<=Multiple entries with same key: )[^=]+", RegexOptions.Compiled);

    static readonly Regex ProvidedByMatch = new("(?<=due to errors, provided by ')[^']+", RegexOptions.Compiled);

    static readonly Regex ModCausedCrashMatch =
        new(@"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+", RegexOptions.Compiled);

    static readonly Regex ConfigFileMatch1 =
        new(@"(?<=Failed loading config file .+ for modid )[^\n]+", RegexOptions.Compiled);

    static readonly Regex ConfigFileMatch2 =
        new("(?<=Failed loading config file ).+(?= of type)", RegexOptions.Compiled);

    static IEnumerable<AnalysisReport.AnalysisReport> ProcessCrashReports(string logs)
    {
        foreach (var causeMap in CrashCausesMap)
        {
            var (matchStr, cause) = causeMap;

            if (logs.Contains(matchStr))
                yield return new AnalysisReport.AnalysisReport(cause);
        }

        // Mod 导致的崩溃
        if (logs.Contains("-- MOD "))
        {
            var modLogs = logs.Split("-- MOD").Last();
            if (modLogs.Contains("Failure message: MISSING"))
                yield return new AnalysisReport.AnalysisReport(CrashCauses.ModCausedGameCrash)
                {
                    Details = new[] { ModFileMatch.Match(logs).Value }
                };
            else
                yield return new AnalysisReport.AnalysisReport(CrashCauses.ModLoaderError)
                {
                    Details = new[] { ModLoaderMatch.Match(logs).Value }
                };
        }

        if (logs.Contains("Multiple entries with same key: "))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.ModCausedGameCrash)
            {
                Details = new[] { MultipleEntriesMatch.Match(logs).Value }
            };
        if (logs.Contains("due to errors, provided by "))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.ModCausedGameCrash)
            {
                Details = new[] { ProvidedByMatch.Match(logs).Value }
            };
        if (logs.Contains("LoaderExceptionModCrash: Caught exception from "))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.ModCausedGameCrash)
            {
                Details = new[] { ModCausedCrashMatch.Match(logs).Value }
            };
        if (logs.Contains("Failed loading config file "))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.IncorrectModConfig)
            {
                Details = new[]
                {
                    ConfigFileMatch1.Match(logs).Value,
                    ConfigFileMatch2.Match(logs).Value
                }
            };
    }

    #endregion

    static IEnumerable<IAnalysisReport> AnalysisLogs(Dictionary<LogFileType, List<(string, string)>> logs)
    {
        var hasGameLogs = logs.TryGetValue(LogFileType.GameLog, out var gameLogs);
        var hasHsErrors = logs.TryGetValue(LogFileType.HsError, out var hsLogs);
        var hasCrashes = logs.TryGetValue(LogFileType.CrashReport, out var crashes);

        if (hasGameLogs)
        {
            foreach (var (fileName, subLogs) in gameLogs!)
            {
                // 找不到或无法加载主类
                if (MainClassMatch1.IsMatch(subLogs) ||
                    MainClassMatch2.IsMatch(subLogs) &&
                    !subLogs.Contains("at net.") || subLogs.Contains("/INFO]") &&
                    !(hasHsErrors && hsLogs!.Any()) &&
                    !(hasCrashes && crashes!.Any()) &&
                    subLogs.Length < 500)
                    yield return
                        new AnalysisReport.AnalysisReport(CrashCauses.IncorrectPathEncodingOrMainClassNotFound);

                foreach (var report in ProcessGameLogs(subLogs))
                    yield return report with { From = fileName };
            }
        }

        if (hasHsErrors)
        {
            foreach (var (fileName, subLogs) in hsLogs!)
            {
                foreach (var report in ProcessHsLogs(subLogs))
                    yield return report with { From = fileName };
            }
        }

        if (hasCrashes)
        {
            foreach (var (fileName, subLogs) in crashes!)
            {
                foreach (var report in ProcessCrashReports(subLogs))
                    yield return report with { From = fileName };
            }
        }
    }

    static readonly Regex WarningsMatch =
        new(@"(?<=\]: Warnings were found! ?[\n]+)[\w\W]+?(?=[\n]+\[)", RegexOptions.Compiled);

    static readonly Regex ModInstanceMatch1 =
        new ("(?<=Failed to create mod instance. ModID: )[^,]+", RegexOptions.Compiled);

    static readonly Regex ModInstanceMatch2 =
        new(@"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )", RegexOptions.Compiled);

    static readonly Regex BlockMatch = new(@"(?<=\tBlock: Block\{)[^\}]+", RegexOptions.Compiled);

    static readonly Regex BlockLocationMatch = new(@"(?<=\tBlock location: World: )\([^\)]+\)", RegexOptions.Compiled);

    static readonly Regex EntityMatch = new(@"(?<=\tEntity Type: )[^\n]+(?= \()", RegexOptions.Compiled);

    static readonly Regex EntityLocationMatch = new(@"(?<=\tEntity's Exact location: )[^\n]+", RegexOptions.Compiled);
        
    static IEnumerable<IAnalysisReport> AnalysisLogs2(Dictionary<LogFileType, List<(string, string)>> logs)
    {
        var hasGameLogs = logs.TryGetValue(LogFileType.GameLog, out var gameLogs);
        var hasCrashes = logs.TryGetValue(LogFileType.CrashReport, out var crashes);
        
        if (hasGameLogs)
        {
            foreach (var (from, log) in gameLogs!)
            {
                if (log.Contains("]: Warnings were found!"))
                    yield return new AnalysisReport.AnalysisReport(CrashCauses.FabricError)
                    {
                        Details = new[] { WarningsMatch.Match(log).Value },
                        From = from
                    };

                if (log.Contains("Failed to create mod instance."))
                    yield return new AnalysisReport.AnalysisReport(CrashCauses.ModInitFailed)
                    {
                        Details = new[]
                        {
                            ModInstanceMatch1.Match(log).Value,
                            ModInstanceMatch2.Match(log).Value
                        },
                        From = from
                    };
            }
        }
        
        if (hasCrashes)
        {
            foreach (var (from, log) in crashes!)
            {
                if (log.Contains("Block location: World: "))
                    yield return new AnalysisReport.AnalysisReport(CrashCauses.BlockCausedGameCrash)
                    {
                        Details = new[]
                        {
                            BlockMatch.Match(log).Value,
                            BlockLocationMatch.Match(log).Value,
                        },
                        From = from
                    };

                if (log.Contains("Entity's Exact location: "))
                    yield return new AnalysisReport.AnalysisReport(CrashCauses.EntityCausedGameCrash)
                    {
                        Details = new []
                        {
                            EntityMatch.Match(log).Value,
                            EntityLocationMatch.Match(log).Value
                        },
                        From = from
                    };
            }
        }
    }

    public IEnumerable<IAnalysisReport> GenerateReport()
    {
        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("未提供 RootPath 参数");
        if (string.IsNullOrEmpty(GameId))
            throw new NullReferenceException("未提供 GameId 参数");

        var logs = new Dictionary<LogFileType, List<(string, string)>>();

        foreach (var (logFileType, lines) in GetAllLogs())
        {
            if(!logs.ContainsKey(logFileType))
                logs[logFileType] = new List<(string, string)>();

            logs[logFileType].Add(lines);
        }

        if (!logs.Any() || logs.All(p => !p.Value.Any()))
            yield return new AnalysisReport.AnalysisReport(CrashCauses.LogFileNotFound);

        var hasLogAnalysisResult = false;
        foreach (var report in AnalysisLogs(logs))
        {
            hasLogAnalysisResult = true;
            yield return report;
        }

        if(hasLogAnalysisResult) yield break;

        foreach (var report in AnalysisLogs2(logs))
            yield return report;
    }
}