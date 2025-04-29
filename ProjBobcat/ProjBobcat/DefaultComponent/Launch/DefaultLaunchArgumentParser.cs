using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.Version;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch;

public sealed class DefaultLaunchArgumentParser : LaunchArgumentParserBase, IArgumentParser
{
    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="launcherProfileParser">Mojang官方launcher_profiles.json适配组件</param>
    /// <param name="versionLocator"></param>
    /// <param name="rootPath"></param>
    public DefaultLaunchArgumentParser(
        ILauncherProfileParser launcherProfileParser,
        IVersionLocator versionLocator,
        string rootPath) : base(rootPath, launcherProfileParser, versionLocator)
    {
        this.VersionLocator = versionLocator;
        this.LauncherProfileParser = launcherProfileParser;
    }

    public IEnumerable<string> ParseJvmHeadArguments(
        LaunchSettings launchSettings,
        GameProfileModel gameProfile)
    {
        var additionalJvmArguments =
            launchSettings.GameArguments.AdditionalJvmArguments ??
            launchSettings.FallBackGameArguments?.AdditionalJvmArguments ??
            [];

        foreach (var jvmArg in additionalJvmArguments)
            yield return jvmArg;

        var minMemory = launchSettings.GameArguments.MinMemory == 0
            ? launchSettings.FallBackGameArguments?.MinMemory ?? 0
            : launchSettings.GameArguments.MinMemory;

        var maxMemory = gameProfile?.MaxMemory ??
                        (launchSettings.GameArguments.MaxMemory == 0
                            ? launchSettings.FallBackGameArguments?.MaxMemory ?? 0
                            : launchSettings.GameArguments.MaxMemory);

        if (maxMemory > 0)
        {
            if (minMemory < maxMemory)
            {
                yield return $"-Xms{minMemory}m";
                yield return $"-Xmx{maxMemory}m";
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

        if (launchSettings.GameArguments.GcType != GcType.Disable)
        {
            var gcArg = launchSettings.GameArguments.GcType switch
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

        if (!string.IsNullOrEmpty(gameProfile?.JavaArgs))
            yield return gameProfile.JavaArgs;
    }

    public IEnumerable<string> ParseJvmArguments(
        string nativePath,
        IVersionInfo versionInfo,
        ResolvedGameVersion resolvedGameVersion,
        LaunchSettings launchSettings)
    {
        var version = (VersionInfo)versionInfo;
        var versionNameFollowing = string.IsNullOrEmpty(version.RootVersion) ? string.Empty : $",{version.RootVersion}";
        var versionName = $"{launchSettings.Version}{versionNameFollowing}".Replace(' ', '_');
        var nativeRoot = Path.Combine(this.RootPath, nativePath);

        var sb = new StringBuilder();
        foreach (var lib in resolvedGameVersion.Libraries)
            sb.Append($"{Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(lib.Path!))}{Path.PathSeparator}");

        var rootJarPath = string.IsNullOrEmpty(version.RootVersion)
            ? GamePathHelper.GetGameExecutablePath(launchSettings.Version)
            : GamePathHelper.GetGameExecutablePath(version.RootVersion);
        var rootJarFullPath = Path.Combine(this.RootPath, rootJarPath);

        if (File.Exists(rootJarFullPath))
            sb.Append(rootJarFullPath);

        var jvmArgumentsDic = new Dictionary<string, string>
        {
            { "${natives_directory}", $"\"{nativeRoot}\"" },
            { "${launcher_name}", $"\"{launchSettings.LauncherName}\"" },
            { "${launcher_version}", "32" },
            { "${classpath}", $"\"{sb}\"" },
            { "${classpath_separator}", Path.PathSeparator.ToString() },
            { "${library_directory}", $"\"{Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath())}\"" },
            { "${version_name}", versionName },
            { "${primary_jar_name}", $"\"{version.Id}.jar\"" }
        };

        #region log4j 缓解措施

        yield return "-Dlog4j2.formatMsgNoLookups=true";

        #endregion

        #region Set Output Encoding

        yield return "-Dfile.encoding=UTF-8";
        yield return "-Dstdout.encoding=UTF-8";
        yield return "-Dstderr.encoding=UTF-8";

        #endregion

        yield return "-Dfml.ignoreInvalidMinecraftCertificates=true";
        yield return "-Dfml.ignorePatchDiscrepancies=true";

        if (resolvedGameVersion.JvmArguments is { Count: > 0 })
        {
            foreach (var jvmArg in resolvedGameVersion.JvmArguments)
            {
                var arg = jvmArg;

                // Patch for PCL2
                if (jvmArg?.Equals("-DFabricMcEmu= net.minecraft.client.main.Main ",
                        StringComparison.OrdinalIgnoreCase) ?? false)
                    arg = "-DFabricMcEmu=net.minecraft.client.main.Main";

                yield return StringHelper.ReplaceByDic(arg, jvmArgumentsDic);
            }
        }
        else
        {
            yield return StringHelper.ReplaceByDic("-Djava.library.path=${natives_directory}", jvmArgumentsDic);
            yield return StringHelper.ReplaceByDic("-Dminecraft.launcher.brand=${launcher_name}", jvmArgumentsDic);
            yield return StringHelper.ReplaceByDic("-Dminecraft.launcher.version=${launcher_version}", jvmArgumentsDic);

            yield return "-cp";
            yield return StringHelper.ReplaceByDic("${classpath}", jvmArgumentsDic);
        }
    }

    public IEnumerable<string> ParseGameArguments(
        IVersionInfo versionInfo,
        ResolvedGameVersion resolvedGameVersion,
        GameProfileModel gameProfile,
        LaunchSettings launchSettings,
        AuthResultBase authResult)
    {
        ArgumentOutOfRangeException.ThrowIfEqual((int)authResult.AuthStatus, (int)AuthStatus.Failed);
        ArgumentOutOfRangeException.ThrowIfEqual((int)authResult.AuthStatus, (int)AuthStatus.Unknown);
        ArgumentNullException.ThrowIfNull(authResult.SelectedProfile);
        ArgumentException.ThrowIfNullOrEmpty(authResult.AccessToken);

        var gameDir = launchSettings.VersionInsulation
            ? Path.Combine(this.RootPath, GamePathHelper.GetGamePath(launchSettings.Version))
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

        var castVersionInfo = (VersionInfo)versionInfo;
        var assetRoot = Path.Combine(this.RootPath, GamePathHelper.GetAssetsRoot());
        var mcArgumentsDic = new Dictionary<string, string>
        {
            { "${version_name}", $"\"{launchSettings.Version}\"" },
            { "${version_type}", $"\"{gameProfile.Type ?? launchSettings.LauncherName}\"" },
            { "${assets_root}", $"\"{assetRoot}\"" },
            {
                "${assets_index_name}",
                resolvedGameVersion.AssetInfo?.Id ?? castVersionInfo.Assets ?? castVersionInfo.Id
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

        foreach (var gameArg in resolvedGameVersion.GameArguments ?? [])
            yield return StringHelper.ReplaceByDic(gameArg, mcArgumentsDic);
    }

    public IReadOnlyList<string> GenerateLaunchArguments(
        string nativePath,
        IVersionInfo versionInfo,
        ResolvedGameVersion resolvedVersion,
        LaunchSettings launchSettings,
        AuthResultBase authResult)
    {
        var gameProfile = this.LauncherProfileParser.GetGameProfile(launchSettings.GameName);

        ArgumentOutOfRangeException.ThrowIfEqual(resolvedVersion, null, nameof(resolvedVersion));

        var arguments = new List<string>();

        arguments.AddRange(this.ParseJvmHeadArguments(launchSettings, gameProfile));
        arguments.AddRange(this.ParseJvmArguments(nativePath, versionInfo, resolvedVersion, launchSettings));

        if (launchSettings.EnableXmlLoggingOutput)
            arguments.AddRange(this.ParseGameLoggingArguments(resolvedVersion));

        arguments.Add(resolvedVersion!.MainClass);

        arguments.AddRange(this.ParseGameArguments(versionInfo, resolvedVersion, gameProfile, launchSettings,
            authResult));
        arguments.AddRange(this.ParseAdditionalArguments(versionInfo, resolvedVersion, launchSettings, gameProfile));

        for (var i = 0; i < arguments.Count; i++)
            arguments[i] = arguments[i].Trim();

        return arguments;
    }

    /// <summary>
    ///     解析 Log4J 日志配置文件相关参数
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> ParseGameLoggingArguments(ResolvedGameVersion version)
    {
        if (version.Logging?.Client == null) yield break;
        if (string.IsNullOrEmpty(version.Logging.Client.File?.Url)) yield break;
        if (string.IsNullOrEmpty(version.Logging?.Client?.Argument)) yield break;

        var fileName = Path.GetFileName(version.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var filePath = Path.Combine(GamePathHelper.GetLoggingPath(this.RootPath), fileName);

        if (!File.Exists(filePath)) yield break;

        var argumentsDic = new Dictionary<string, string>
        {
            { "${path}", $"\"{filePath}\"" }
        };

        yield return StringHelper.ReplaceByDic(version.Logging.Client.Argument, argumentsDic);
    }

    /// <summary>
    ///     解析额外参数（分辨率，服务器地址）
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> ParseAdditionalArguments(
        IVersionInfo versionInfo,
        ResolvedGameVersion version,
        LaunchSettings launchSettings,
        GameProfileModel gameProfile)
    {
        if ((version.AvailableGameArguments?.Count ?? 0) == 0) yield break;
        if (!version.AvailableGameArguments!.ContainsKey("has_custom_resolution")) yield break;

        if (!(launchSettings.GameArguments.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return launchSettings.GameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return launchSettings.GameArguments.Resolution.Height.ToString();
        }
        else if (!(launchSettings.FallBackGameArguments?.Resolution?.IsDefault() ?? true))
        {
            yield return "--width";
            yield return launchSettings.FallBackGameArguments.Resolution.Width.ToString();

            yield return "--height";
            yield return launchSettings.FallBackGameArguments.Resolution.Height.ToString();
        }
        else if (!gameProfile.Resolution!.IsDefault())
        {
            yield return "--width";
            yield return gameProfile.Resolution.Width.ToString();

            yield return "--height";
            yield return gameProfile.Resolution.Height.ToString();
        }

        if (launchSettings.GameArguments.ServerSettings == null &&
            launchSettings.FallBackGameArguments?.ServerSettings == null) yield break;

        var serverSettings = launchSettings.GameArguments.ServerSettings ??
                             launchSettings.FallBackGameArguments?.ServerSettings;
        var joinWorldName = launchSettings.GameArguments.JoinWorldName ??
                            launchSettings.FallBackGameArguments?.JoinWorldName;

        // Starting from 1.20, we need to use the new command line arguments
        var newFormatVersionLimit = new ComparableVersion("1.20");
        var gameVersion = new ComparableVersion(((VersionInfo)versionInfo).GameBaseVersion);
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

        if (!string.IsNullOrEmpty(launchSettings.GameArguments.AdvanceArguments))
            yield return launchSettings.GameArguments.AdvanceArguments;
        else if (!string.IsNullOrEmpty(launchSettings.FallBackGameArguments?.AdvanceArguments))
            yield return launchSettings.FallBackGameArguments.AdvanceArguments;
    }
}