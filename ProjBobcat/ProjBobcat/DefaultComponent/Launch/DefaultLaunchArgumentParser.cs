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
using ProjBobcat.Class.Model.Version;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch;

public class DefaultLaunchArgumentParser : LaunchArgumentParserBase, IArgumentParser
{
    readonly LaunchSettings _launchSettings;
    readonly string? _rootVersion;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="launchSettings">启动设置</param>
    /// <param name="launcherProfileParser">Mojang官方launcher_profiles.json适配组件</param>
    /// <param name="versionLocator"></param>
    /// <param name="authResult"></param>
    /// <param name="rootPath"></param>
    /// <param name="rootVersion"></param>
    public DefaultLaunchArgumentParser(
        LaunchSettings launchSettings,
        ILauncherProfileParser launcherProfileParser,
        IVersionLocator versionLocator,
        AuthResultBase authResult,
        string rootPath,
        string? rootVersion) : base(rootPath, launchSettings, launcherProfileParser, versionLocator, authResult)
    {
        ArgumentNullException.ThrowIfNull(launchSettings);
        ArgumentNullException.ThrowIfNull(launcherProfileParser);

        this._launchSettings = launchSettings;
        this._rootVersion = rootVersion;

        this.AuthResult = authResult;
        this.VersionLocator = versionLocator;
        this.RootPath = rootPath;
        this.LaunchSettings = launchSettings;
        this.LauncherProfileParser = launcherProfileParser;
        this.VersionInfo = this.LaunchSettings.VersionLocator.GetGame(this.LaunchSettings.Version)
                           ?? throw new UnknownGameNameException(this.LaunchSettings.Version);
        this.GameProfile = this.LauncherProfileParser.GetGameProfile(this.LaunchSettings.GameName);

        var sb = new StringBuilder();
        foreach (var lib in this.VersionInfo.Libraries)
            sb.Append($"{Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(lib.Path!))}{Path.PathSeparator}");

        if (true)
        {
            var rootJarPath = string.IsNullOrEmpty(rootVersion)
                ? GamePathHelper.GetGameExecutablePath(launchSettings.Version)
                : GamePathHelper.GetGameExecutablePath(rootVersion);
            var rootJarFullPath = Path.Combine(this.RootPath, rootJarPath);

            if (File.Exists(rootJarFullPath))
                sb.Append(rootJarFullPath);
        }

        this.ClassPath = sb.ToString();
        this.LastAuthResult = this.LaunchSettings.Authenticator.GetLastAuthResult();
    }

    public bool EnableXmlLoggingOutput { get; init; }

    protected override string ClassPath { get; init; }
    protected override VersionInfo VersionInfo { get; init; }

    public IEnumerable<string> ParseJvmHeadArguments()
    {
        ArgumentNullException.ThrowIfNull(this.LaunchSettings);

        if (this.LaunchSettings.GameArguments == null && this.LaunchSettings.FallBackGameArguments == null)
            throw new ArgumentNullException("重要参数为 Null!");

        var gameArgs = this.LaunchSettings.GameArguments ?? this.LaunchSettings.FallBackGameArguments;

        if ((gameArgs!.AdditionalJvmArguments?.Count ?? 0) > 0)
            foreach (var jvmArg in gameArgs.AdditionalJvmArguments!)
                yield return jvmArg;

        if (string.IsNullOrEmpty(this.GameProfile?.JavaArgs))
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
            yield return this.GameProfile.JavaArgs;
        }

        /*
        sb.Append(
                "-XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M")
            .Append(' ');
        */
    }

    public IEnumerable<string> ParseJvmArguments()
    {
        var versionNameFollowing = string.IsNullOrEmpty(this._rootVersion) ? string.Empty : $",{this._rootVersion}";
        var versionName = $"{this.LaunchSettings.Version}{versionNameFollowing}".Replace(' ', '_');

        var jvmArgumentsDic = new Dictionary<string, string>
        {
            { "${natives_directory}", $"\"{this.NativeRoot}\"" },
            { "${launcher_name}", $"\"{this.LaunchSettings.LauncherName}\"" },
            { "${launcher_version}", "32" },
            { "${classpath}", $"\"{this.ClassPath}\"" },
            { "${classpath_separator}", Path.PathSeparator.ToString() },
            { "${library_directory}", $"\"{Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath())}\"" },
            { "${version_name}", versionName }
        };

        #region log4j 缓解措施

        yield return "-Dlog4j2.formatMsgNoLookups=true";

        #endregion

        yield return "-Dfml.ignoreInvalidMinecraftCertificates=true";
        yield return "-Dfml.ignorePatchDiscrepancies=true";

        if (this.VersionInfo.JvmArguments?.Any() ?? false)
        {
            foreach (var jvmArg in this.VersionInfo.JvmArguments)
            {
                var arg = jvmArg;

                // Patch for PCL2
                if (jvmArg?.Equals("-DFabricMcEmu= net.minecraft.client.main.Main ",
                        StringComparison.OrdinalIgnoreCase) ?? false)
                    arg = "-DFabricMcEmu=net.minecraft.client.main.Main";

                yield return StringHelper.ReplaceByDic(arg, jvmArgumentsDic);
            }

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

        var preJvmArguments = this.VersionLocator.ParseJvmArguments(JsonSerializer.Deserialize(preset,
            JsonElementContext.Default.JsonElementArray)!);

        foreach (var preJvmArg in preJvmArguments)
            yield return StringHelper.ReplaceByDic(preJvmArg, jvmArgumentsDic);
    }

    public IEnumerable<string> ParseGameArguments(AuthResultBase authResult)
    {
        if (authResult.AuthStatus == AuthStatus.Failed ||
            authResult.AuthStatus == AuthStatus.Unknown ||
            authResult.SelectedProfile == null ||
            string.IsNullOrEmpty(authResult.AccessToken))
            throw new ArgumentNullException("无效的用户凭据，请检查登陆状态");

        var gameDir = this._launchSettings.VersionInsulation
            ? Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.LaunchSettings.Version))
            : this.RootPath;
        var clientIdUpper = (this.VersionLocator?.LauncherProfileParser?.LauncherProfile?.ClientToken ??
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
            ? microsoftAuthResult.XBoxUid ?? Guid.Empty.ToString("N")
            : Guid.Empty.ToString("N");

        var mcArgumentsDic = new Dictionary<string, string>
        {
            { "${version_name}", $"\"{this.LaunchSettings.Version}\"" },
            { "${version_type}", $"\"{this.GameProfile?.Type ?? this.LaunchSettings.LauncherName}\"" },
            { "${assets_root}", $"\"{this.AssetRoot}\"" },
            {
                "${assets_index_name}", this.VersionInfo.AssetInfo?.Id ?? this.VersionInfo.Assets ?? this.VersionInfo.Id
            },
            { "${game_directory}", $"\"{gameDir}\"" },
            { "${auth_player_name}", authResult.SelectedProfile.Name },
            { "${auth_uuid}", authResult.SelectedProfile.UUID.ToString() },
            { "${auth_access_token}", authResult.AccessToken },
            { "${user_properties}", "{}" }, //authResult?.User?.Properties.ResolveUserProperties() },
            { "${user_type}", userType }, // use default value as placeholder
            { "${clientid}", clientId },
            { "${auth_xuid}", xuid }
        };

        foreach (var gameArg in this.VersionInfo.GameArguments)
            yield return StringHelper.ReplaceByDic(gameArg, mcArgumentsDic);
    }

    public List<string> GenerateLaunchArguments()
    {
        var javaPath = this.GameProfile?.JavaDir;
        if (string.IsNullOrEmpty(javaPath))
            javaPath = this.LaunchSettings.FallBackGameArguments?.JavaExecutable ??
                       this.LaunchSettings.GameArguments.JavaExecutable;

        var arguments = new List<string>
        {
            javaPath
        };

        arguments.AddRange(this.ParseJvmHeadArguments());
        arguments.AddRange(this.ParseJvmArguments());

        if (this.EnableXmlLoggingOutput)
            arguments.AddRange(this.ParseGameLoggingArguments());

        arguments.Add(this.VersionInfo.MainClass);

        arguments.AddRange(this.ParseGameArguments(this.AuthResult));
        arguments.AddRange(this.ParseAdditionalArguments());

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
        if (this.VersionInfo.Logging?.Client == null) yield break;
        if (string.IsNullOrEmpty(this.VersionInfo.Logging.Client.File?.Url)) yield break;
        if (string.IsNullOrEmpty(this.VersionInfo.Logging?.Client?.Argument)) yield break;

        var fileName = Path.GetFileName(this.VersionInfo.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var filePath = Path.Combine(GamePathHelper.GetLoggingPath(this.RootPath), fileName);

        if (!File.Exists(filePath)) yield break;

        var argumentsDic = new Dictionary<string, string>
        {
            { "${path}", $"\"{filePath}\"" }
        };

        yield return StringHelper.ReplaceByDic(this.VersionInfo.Logging.Client.Argument, argumentsDic);
    }

    /// <summary>
    ///     解析额外参数（分辨率，服务器地址）
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> ParseAdditionalArguments()
    {
        if ((this.VersionInfo.AvailableGameArguments?.Count ?? 0) == 0) yield break;
        if (!this.VersionInfo.AvailableGameArguments!.ContainsKey("has_custom_resolution")) yield break;

        if (!(this.LaunchSettings.GameArguments.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return this.LaunchSettings.GameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return this.LaunchSettings.GameArguments.Resolution.Height.ToString();
        }
        else if (!(this.LaunchSettings.FallBackGameArguments?.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return this.LaunchSettings.FallBackGameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return this.LaunchSettings.FallBackGameArguments.Resolution.Height.ToString();
        }
        else if (!this.GameProfile!.Resolution!.IsDefault())
        {
            yield return "--width";
            yield return this.GameProfile.Resolution.Width.ToString();

            yield return "--height";
            yield return this.GameProfile.Resolution.Height.ToString();
        }

        if (this.LaunchSettings.GameArguments.ServerSettings == null &&
            this.LaunchSettings.FallBackGameArguments?.ServerSettings == null) yield break;

        var serverSettings = this.LaunchSettings.GameArguments.ServerSettings ??
                             this.LaunchSettings.FallBackGameArguments?.ServerSettings;
        var joinWorldName = LaunchSettings.GameArguments.JoinWorldName ??
                                LaunchSettings.FallBackGameArguments?.JoinWorldName;

        // Starting from 1.20, we need to use the new command line arguments
        var newFormatVersionLimit = new ComparableVersion("1.20");
        var gameVersion = new ComparableVersion(VersionInfo.GameBaseVersion);
        var shouldUseNewCommand = gameVersion >= newFormatVersionLimit;

        if (serverSettings != null && !serverSettings.IsDefault())
        {
            if (string.IsNullOrEmpty(serverSettings.Address)) yield break;

            if (shouldUseNewCommand)
            {
                yield return $"--quickPlayMultiplayer \"{serverSettings.Address}:{serverSettings.Port}\"";
            }
            else
            {
                yield return "--server";
                yield return serverSettings.Address;

                yield return "--port";
                yield return serverSettings.Port.ToString();
            }
        }
        else if (!string.IsNullOrEmpty(joinWorldName) && shouldUseNewCommand)
        {
            yield return $"--quickPlaySingleplayer \"{joinWorldName}\"";
        }

        if (!string.IsNullOrEmpty(this.LaunchSettings.GameArguments.AdvanceArguments))
            yield return this.LaunchSettings.GameArguments.AdvanceArguments;
        else if (!string.IsNullOrEmpty(this.LaunchSettings.FallBackGameArguments?.AdvanceArguments))
            yield return this.LaunchSettings.FallBackGameArguments.AdvanceArguments;
    }
}