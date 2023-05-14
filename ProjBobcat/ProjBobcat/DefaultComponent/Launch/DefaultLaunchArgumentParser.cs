using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch;

public class DefaultLaunchArgumentParser : LaunchArgumentParserBase, IArgumentParser
{
    readonly LaunchSettings _launchSettings;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="launchSettings">启动设置</param>
    /// <param name="launcherProfileParser">Mojang官方launcher_profiles.json适配组件</param>
    /// <param name="versionLocator"></param>
    /// <param name="authResult"></param>
    /// <param name="rootPath"></param>
    /// <param name="rootVersion"></param>
    public DefaultLaunchArgumentParser(LaunchSettings launchSettings, ILauncherProfileParser launcherProfileParser,
        IVersionLocator versionLocator, AuthResultBase authResult, string rootPath, string rootVersion)
    {
        if (launchSettings == null || launcherProfileParser == null)
            throw new ArgumentNullException();

        _launchSettings = launchSettings;

        AuthResult = authResult;
        VersionLocator = versionLocator;
        RootPath = rootPath;
        LaunchSettings = launchSettings;
        LauncherProfileParser = launcherProfileParser;
        VersionInfo = LaunchSettings.VersionLocator.GetGame(LaunchSettings.Version);
        GameProfile = LauncherProfileParser.GetGameProfile(LaunchSettings.GameName);

        var sb = new StringBuilder();
        foreach (var lib in VersionInfo.Libraries)
            sb.Append($"{Path.Combine(RootPath, GamePathHelper.GetLibraryPath(lib.Path))}{Path.PathSeparator}");


        if (!VersionInfo.MainClass.Equals("cpw.mods.bootstraplauncher.BootstrapLauncher",
                StringComparison.OrdinalIgnoreCase))
        {
            var rootJarPath = string.IsNullOrEmpty(rootVersion)
                ? GamePathHelper.GetGameExecutablePath(launchSettings.Version)
                : GamePathHelper.GetGameExecutablePath(rootVersion);
            var rootJarFullPath = Path.Combine(RootPath, rootJarPath);

            if (File.Exists(rootJarFullPath))
                sb.Append(rootJarFullPath);
        }

        ClassPath = sb.ToString();
        LastAuthResult = LaunchSettings.Authenticator.GetLastAuthResult();
    }

    public bool EnableXmlLoggingOutput { get; init; }

    public IEnumerable<string> ParseJvmHeadArguments()
    {
        if (LaunchSettings == null ||
            (LaunchSettings.GameArguments == null && LaunchSettings.FallBackGameArguments == null))
            throw new ArgumentNullException("重要参数为Null!");

        var gameArgs = LaunchSettings.GameArguments ?? LaunchSettings.FallBackGameArguments;

        if (gameArgs.AdditionalJvmArguments?.Any() ?? false)
            foreach (var jvmArg in gameArgs.AdditionalJvmArguments)
                yield return jvmArg;

        if (string.IsNullOrEmpty(GameProfile?.JavaArgs))
        {
            if (gameArgs.MaxMemory > 0)
            {
                if (gameArgs.MinMemory < gameArgs.MaxMemory)
                {
                    yield return $"-Xms{gameArgs.MinMemory}m";
                    yield return $"-Xmx{gameArgs.MaxMemory}m";
                }
                else
                {
                    yield return "-Xmx2G";
                }
            }
            else
            {
                yield return "-Xmx2G";
            }


            if (gameArgs.GcType == GcType.Disable)
                yield break;

            var gcArg = gameArgs.GcType switch
            {
                GcType.CmsGc => "-XX:+UseConcMarkSweepGC",
                GcType.G1Gc => "-XX:+UseG1GC",
                GcType.ParallelGc => "-XX:+UseParallelGC",
                GcType.SerialGc => "-XX:+UseSerialGC",
                GcType.ZGc => "-XX:+UseZGC",
                _ => "-XX:+UseG1GC"
            };

            yield return gcArg;
        }
        else
        {
            yield return GameProfile.JavaArgs;
        }

        /*
        sb.Append(
                "-XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M")
            .Append(' ');
        */
    }

    public IEnumerable<string> ParseJvmArguments()
    {
        var jvmArgumentsDic = new Dictionary<string, string>
        {
            { "${natives_directory}", $"\"{NativeRoot}\"" },
            { "${launcher_name}", $"\"{LaunchSettings.LauncherName}\"" },
            { "${launcher_version}", "32" },
            { "${classpath}", $"\"{ClassPath}\"" },
            { "${classpath_separator}", Path.PathSeparator.ToString() },
            { "${library_directory}", $"\"{Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath())}\"" }
        };

        #region log4j 缓解措施

        yield return "-Dlog4j2.formatMsgNoLookups=true";

        #endregion

        yield return "-Dfml.ignoreInvalidMinecraftCertificates=true";
        yield return "-Dfml.ignorePatchDiscrepancies=true";

        if (VersionInfo.JvmArguments?.Any() ?? false)
        {
            foreach (var jvmArg in VersionInfo.JvmArguments)
                yield return StringHelper.ReplaceByDic(jvmArg, jvmArgumentsDic);
            yield break;
        }

        const string preset = """
            [
                {
                    "rules": [
                        {
                            "action": "allow",
                            "os": {
                                "name": "osx"
                            }
                        }
                    ],
                    "value": [
                        "-XstartOnFirstThread"
                    ]
                },
                {
                    "rules": [
                        {
                            "action": "allow",
                            "os": {
                                "name": "windows"
                            }
                        }
                    ],
                    "value": "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump"
                },
                {
                    "rules": [
                        {
                            "action": "allow",
                            "os": {
                                "name": "windows",
                                "version": "^10\\\\."
                            }
                        }
                    ],
                    "value": [
                        "-Dos.name=Windows 10",
                        "-Dos.version=10.0"
                    ]
                },
                "-Djava.library.path=${natives_directory}",
                "-Dminecraft.launcher.brand=${launcher_name}",
                "-Dminecraft.launcher.version=${launcher_version}",
                "-cp",
                "${classpath}"
            ]
            """;

        var preJvmArguments =
            VersionLocator.ParseJvmArguments(JsonSerializer.Deserialize(preset,
                JsonElementContext.Default.JsonElementArray)!);

        foreach (var preJvmArg in preJvmArguments)
            yield return StringHelper.ReplaceByDic(preJvmArg, jvmArgumentsDic);
    }

    public IEnumerable<string> ParseGameArguments(AuthResultBase authResult)
    {
        var gameDir = _launchSettings.VersionInsulation
            ? Path.Combine(RootPath, GamePathHelper.GetGamePath(LaunchSettings.Version))
            : RootPath;
        var clientIdUpper = (VersionLocator?.LauncherProfileParser?.LauncherProfile?.ClientToken ??
                             Guid.Empty.ToString("D"))
            .Replace("-", string.Empty).ToUpper();
        var clientIdBytes = Encoding.ASCII.GetBytes(clientIdUpper);
        var clientId = Convert.ToBase64String(clientIdBytes);

        var userType = authResult switch
        {
            MicrosoftAuthResult => "msa",
            _ => "Mojang"
        };
        var xuid = authResult is MicrosoftAuthResult microsoftAuthResult
            ? microsoftAuthResult.XBoxUid
            : Guid.Empty.ToString("N");

        var mcArgumentsDic = new Dictionary<string, string>
        {
            { "${version_name}", $"\"{LaunchSettings.Version}\"" },
            { "${version_type}", $"\"{GameProfile?.Type ?? LaunchSettings.LauncherName}\"" },
            { "${assets_root}", $"\"{AssetRoot}\"" },
            { "${assets_index_name}", VersionInfo.AssetInfo?.Id ?? VersionInfo.Assets },
            { "${game_directory}", $"\"{gameDir}\"" },
            { "${auth_player_name}", authResult?.SelectedProfile?.Name },
            { "${auth_uuid}", authResult?.SelectedProfile?.UUID.ToString() },
            { "${auth_access_token}", authResult?.AccessToken },
            { "${user_properties}", "{}" }, //authResult?.User?.Properties.ResolveUserProperties() },
            { "${user_type}", userType }, // use default value as placeholder
            { "${clientid}", clientId },
            { "${auth_xuid}", xuid }
        };

        foreach (var gameArg in VersionInfo.GameArguments)
            yield return StringHelper.ReplaceByDic(gameArg, mcArgumentsDic);
    }

    public List<string> GenerateLaunchArguments()
    {
        var javaPath = GameProfile?.JavaDir;
        if (string.IsNullOrEmpty(javaPath))
            javaPath = LaunchSettings.FallBackGameArguments?.JavaExecutable ??
                       LaunchSettings.GameArguments?.JavaExecutable;

        var arguments = new List<string>
        {
            javaPath
        };

        arguments.AddRange(ParseJvmHeadArguments());
        arguments.AddRange(ParseJvmArguments());

        if (EnableXmlLoggingOutput)
            arguments.AddRange(ParseGameLoggingArguments());

        arguments.Add(VersionInfo.MainClass);

        arguments.AddRange(ParseGameArguments(AuthResult));
        arguments.AddRange(ParseAdditionalArguments());

        for (var i = 0; i < arguments.Count; i++)
            arguments[i] = arguments[i].Trim();

        return arguments;
    }

    /// <summary>
    ///     解析 Log4J 日志配置文件相关参数
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> ParseGameLoggingArguments()
    {
        if (VersionInfo.Logging?.Client == null) yield break;
        if (string.IsNullOrEmpty(VersionInfo.Logging.Client.File?.Url)) yield break;
        if (string.IsNullOrEmpty(VersionInfo.Logging?.Client?.Argument)) yield break;

        var fileName = Path.GetFileName(VersionInfo.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var filePath = Path.Combine(GamePathHelper.GetLoggingPath(RootPath), fileName);

        if (!File.Exists(filePath)) yield break;

        var argumentsDic = new Dictionary<string, string>
        {
            { "${path}", $"\"{filePath}\"" }
        };

        yield return StringHelper.ReplaceByDic(VersionInfo.Logging.Client.Argument, argumentsDic);
    }

    /// <summary>
    ///     解析额外参数（分辨率，服务器地址）
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> ParseAdditionalArguments()
    {
        if (!VersionInfo.AvailableGameArguments.Any()) yield break;
        if (!VersionInfo.AvailableGameArguments.ContainsKey("has_custom_resolution")) yield break;

        if (!(LaunchSettings.GameArguments?.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return LaunchSettings.GameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return LaunchSettings.GameArguments.Resolution.Height.ToString();
        }
        else if (!(LaunchSettings.FallBackGameArguments?.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return LaunchSettings.FallBackGameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return LaunchSettings.FallBackGameArguments.Resolution.Height.ToString();
        }
        else if (!(GameProfile.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return GameProfile.Resolution.Width.ToString();

            yield return "--height";
            yield return GameProfile.Resolution.Height.ToString();
        }

        if (LaunchSettings.GameArguments?.ServerSettings == null &&
            LaunchSettings.FallBackGameArguments?.ServerSettings == null) yield break;

        var serverSettings = LaunchSettings.GameArguments?.ServerSettings ??
                             LaunchSettings.FallBackGameArguments?.ServerSettings;

        if (serverSettings != null && !serverSettings.IsDefault())
        {
            if (string.IsNullOrEmpty(serverSettings.Address)) yield break;

            yield return "--server";
            yield return serverSettings.Address;

            yield return "--port";
            yield return serverSettings.Port.ToString();
        }
    }
}