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

        var maxMemory = (launchSettings.IgnoreLauncherProfileSettings ? null : gameProfile.MaxMemory) ??
                        (launchSettings.GameArguments.MaxMemory == 0
                            ? launchSettings.FallBackGameArguments?.MaxMemory ?? 0
                            : launchSettings.GameArguments.MaxMemory);

        if (maxMemory > 0)
        {
            if (minMemory <= maxMemory)
            {
                if (minMemory > 0) yield return $"-Xms{minMemory}m";
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

        if (!string.IsNullOrEmpty(gameProfile.JavaArgs))
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
        var versionName = $"{launchSettings.Version}{versionNameFollowing}";
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
            { "${natives_directory}", nativeRoot },
            { "${launcher_name}", launchSettings.LauncherName ?? "ProjBobcat" },
            { "${launcher_version}", "32" },
            { "${classpath}", sb.ToString() },
            { "${classpath_separator}", Path.PathSeparator.ToString() },
            { "${library_directory}", Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath()) },
            { "${version_name}", versionName },
            { "${primary_jar_name}", $"{version.Id}.jar" }
        };

        #region Old 1.16.5 game multiplayer fix apply

        if (launchSettings.AutoApplyFixForOldMultiPlayerGame &&
            !string.IsNullOrEmpty(launchSettings.OldGameTrustStorePath) &&
            File.Exists(launchSettings.OldGameTrustStorePath))
        {
            yield return $"-Djavax.net.ssl.trustStore=\"{launchSettings.OldGameTrustStorePath}\"";
            yield return "-Djavax.net.ssl.trustStorePassword=changeit";
            yield return "-Djdk.tls.client.protocols=TLSv1.2";
        }

        #endregion

        #region log4j 缓解措施

        yield return "-Dlog4j2.formatMsgNoLookups=true";

        #endregion

        #region Set Output Encoding

        var encoding = EncodingHelper.GetUtf8NoBomOrAnsi(launchSettings.PreferUtf8Encoding);
        var encodingArg = EncodingHelper.GetJavaCharsetForAnsi(encoding.CodePage);

        yield return $"-Dfile.encoding={encodingArg}";
        yield return $"-Dstdout.encoding={encodingArg}";
        yield return $"-Dstderr.encoding={encodingArg}";
        yield return $"-Dsun.stdout.encoding={encodingArg}";
        yield return $"-Dsun.stderr.encoding={encodingArg}";

        #endregion

        yield return "-Dfml.ignoreInvalidMinecraftCertificates=true";
        yield return "-Dfml.ignorePatchDiscrepancies=true";

        if (launchSettings.UseV4NetworkingStack)
            yield return "-Djava.net.preferIPv4Stack=true";

        if (resolvedGameVersion.JvmArguments is { Count: > 0 })
        {
            foreach (var jvmArg in resolvedGameVersion.JvmArguments)
            {
                var arg = jvmArg;

                // Patch for PCL2
                if (jvmArg.Equals("-DFabricMcEmu= net.minecraft.client.main.Main ", StringComparison.OrdinalIgnoreCase))
                    arg = "-DFabricMcEmu=net.minecraft.client.main.Main";

                yield return StringHelper.FixArgument(StringHelper.ReplaceByDic(arg, jvmArgumentsDic));
            }
        }
        else
        {
            yield return StringHelper.FixArgument(StringHelper.ReplaceByDic("-Djava.library.path=${natives_directory}",
                jvmArgumentsDic));
            yield return StringHelper.FixArgument(
                StringHelper.ReplaceByDic("-Dminecraft.launcher.brand=${launcher_name}", jvmArgumentsDic));
            yield return StringHelper.FixArgument(
                StringHelper.ReplaceByDic("-Dminecraft.launcher.version=${launcher_version}", jvmArgumentsDic));

            yield return "-cp";
            yield return StringHelper.FixArgument(StringHelper.ReplaceByDic("${classpath}", jvmArgumentsDic));
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
        var clientIdUpper = (this.VersionLocator.LauncherProfileParser?.LauncherProfile.ClientToken ??
                             Guid.Empty.ToString("D"))
            .Replace("-", string.Empty).ToUpper();
        var clientIdBytes = Encoding.ASCII.GetBytes(clientIdUpper);
        var clientId = Convert.ToBase64String(clientIdBytes);

        var castVersionInfo = (VersionInfo)versionInfo;

        var userType = authResult switch
        {
            MicrosoftAuthResult => "msa",
            YggdrasilAuthResult when new ComparableVersion(castVersionInfo.GameBaseVersion) >
                                     new ComparableVersion("1.18.2") => "msa",
            _ => "Mojang"
        };
        var xuid = authResult is MicrosoftAuthResult microsoftAuthResult
            ? microsoftAuthResult.XBoxUid ?? Guid.Empty.ToString("N")
            : Guid.Empty.ToString("N");


        var assetRoot = Path.Combine(this.RootPath, GamePathHelper.GetAssetsRoot());
        var mcArgumentsDic = new Dictionary<string, string>
        {
            { "${version_name}", $"\"{launchSettings.Version}\"" },
            { "${version_type}", $"\"{(launchSettings.IgnoreLauncherProfileSettings ? null : gameProfile.Type) ?? launchSettings.LauncherName}\"" },
            { "${assets_root}", $"\"{assetRoot}\"" },
            {
                "${assets_index_name}",
                resolvedGameVersion.AssetInfo?.Id ?? castVersionInfo.Assets ?? castVersionInfo.Id
            },
            { "${game_directory}", $"\"{gameDir}\"" },
            { "${auth_player_name}", authResult.SelectedProfile.Name },
            { "${auth_uuid}", authResult.SelectedProfile.Id.ToString("N") },
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
        var gameProfile = launchSettings.IgnoreLauncherProfileSettings
            ? new GameProfileModel()
            : this.LauncherProfileParser.GetGameProfile(launchSettings.GameName);

        ArgumentOutOfRangeException.ThrowIfEqual(resolvedVersion, null);

        var arguments = new List<string>();

        arguments.AddRange(this.ParseJvmHeadArguments(launchSettings, gameProfile));
        arguments.AddRange(this.ParseJvmArguments(nativePath, versionInfo, resolvedVersion, launchSettings));

        if (launchSettings.EnableXmlLoggingOutput)
            arguments.AddRange(this.ParseGameLoggingArguments(resolvedVersion));

        arguments.Add(resolvedVersion.MainClass);

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
            { "${path}", filePath }
        };

        yield return StringHelper.FixArgument(StringHelper.ReplaceByDic(version.Logging.Client.Argument, argumentsDic));
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
        var resolution = launchSettings.IgnoreLauncherProfileSettings
            ? launchSettings.GameArguments.Resolution ?? launchSettings.FallBackGameArguments?.Resolution
            : !(launchSettings.GameArguments.Resolution?.IsDefault() ?? true)
                ? launchSettings.GameArguments.Resolution
                : !(launchSettings.FallBackGameArguments?.Resolution?.IsDefault() ?? true)
                    ? launchSettings.FallBackGameArguments.Resolution : gameProfile.Resolution;
        if (resolution?.FullScreen == true) yield return "--fullscreen";
        if (version.AvailableGameArguments?.ContainsKey("has_custom_resolution") == true)
        {
            if (!(resolution?.IsDefault() ?? true))
            {
                yield return "--width";
                yield return resolution!.Width.ToString();
                yield return "--height";
                yield return resolution.Height.ToString();
            }
        }

        var server = launchSettings.GameArguments.ServerSettings ?? launchSettings.FallBackGameArguments?.ServerSettings;
        var world = launchSettings.GameArguments.JoinWorldName ?? launchSettings.FallBackGameArguments?.JoinWorldName;
        foreach (var argument in ParseJoinArguments(((VersionInfo)versionInfo).GameBaseVersion,
                     version.AvailableGameArguments, server, world)) yield return argument;

        var extraArguments = launchSettings.GameArguments.AdditionalGameArguments
                             ?? launchSettings.FallBackGameArguments?.AdditionalGameArguments;
        if (extraArguments is not null)
        {
            if (launchSettings.UseShellExecute && extraArguments.Count > 0)
                throw new NotSupportedException("Tokenized game arguments require direct process launch.");
            foreach (var argument in extraArguments) yield return QuoteProcessArgument(argument);
            yield break;
        }

        if (!string.IsNullOrEmpty(launchSettings.GameArguments.AdvanceArguments))
            yield return launchSettings.GameArguments.AdvanceArguments;
        else if (!string.IsNullOrEmpty(launchSettings.FallBackGameArguments?.AdvanceArguments))
            yield return launchSettings.FallBackGameArguments.AdvanceArguments;
    }

    /// <summary>Splits user-entered process arguments using double quotes and
    /// backslash-before-quote rules, without evaluating shell syntax.</summary>
    public static IReadOnlyList<string> ParseCommandLineArguments(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return [];
        if (commandLine.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new ArgumentException("Invalid process arguments.", nameof(commandLine));
        var result = new List<string>();
        var token = new StringBuilder();
        var quoted = false;
        var started = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (!quoted && char.IsWhiteSpace(character))
            {
                if (started) { result.Add(token.ToString()); token.Clear(); started = false; }
                continue;
            }
            started = true;
            if (character == '\\')
            {
                var slashes = 1;
                while (index + 1 < commandLine.Length && commandLine[index + 1] == '\\')
                { slashes++; index++; }
                if (index + 1 < commandLine.Length && commandLine[index + 1] == '\"')
                {
                    token.Append('\\', slashes / 2);
                    index++;
                    if (slashes % 2 != 0) token.Append('\"');
                    else quoted = !quoted;
                }
                else token.Append('\\', slashes);
            }
            else if (character == '\"') quoted = !quoted;
            else token.Append(character);
        }
        if (quoted) throw new ArgumentException("Unclosed quote in process arguments.", nameof(commandLine));
        if (started) result.Add(token.ToString());
        return result;
    }

    /// <summary>Quotes one token for ProcessStartInfo.Arguments, preserving whitespace,
    /// literal quotes and backslashes. This is not a shell-command encoder.</summary>
    public static string QuoteProcessArgument(string argument)
    {
        if (argument.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new ArgumentException("Invalid process argument.", nameof(argument));
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { backslashes++; continue; }
            if (character == '\"')
            {
                result.Append('\\', backslashes * 2 + 1).Append(character);
                backslashes = 0;
                continue;
            }
            result.Append('\\', backslashes).Append(character);
            backslashes = 0;
        }
        return result.Append('\\', backslashes * 2).Append('\"').ToString();
    }

    public static bool SupportsQuickPlay(string? gameVersion,
        IReadOnlyDictionary<string, string>? features, bool singleplayer)
    {
        var feature = singleplayer ? "is_quick_play_singleplayer" : "is_quick_play_multiplayer";
        if (features?.ContainsKey(feature) == true) return true;
        // Feature declarations cover snapshots; numeric versions cover older metadata from mod loaders.
        return Version.TryParse(gameVersion, out var version) && version >= new Version(1, 20);
    }

    public static IEnumerable<string> ParseJoinArguments(string? gameVersion,
        IReadOnlyDictionary<string, string>? features, ServerSettings? server, string? world)
    {
        if (server is not null && !server.IsDefault() && !string.IsNullOrEmpty(server.Address))
        {
            var address = server.Address;
            if (address.IndexOfAny(['"', '\r', '\n', '\0']) >= 0) throw new ArgumentException("Invalid server address.");
            if (SupportsQuickPlay(gameVersion, features, false))
            {
                if (address.Contains(':') && !address.StartsWith('[')) address = $"[{address}]";
                yield return "--quickPlayMultiplayer";
                yield return StringHelper.FixArgument(server.Port == 0 ? address : $"{address}:{server.Port}");
            }
            else
            {
                yield return "--server";
                yield return StringHelper.FixArgument(address);
                if (server.Port != 0)
                {
                    yield return "--port";
                    yield return server.Port.ToString();
                }
            }
        }
        else if (!string.IsNullOrEmpty(world))
        {
            if (!SupportsQuickPlay(gameVersion, features, true))
                throw new NotSupportedException("This game version does not support single-player Quick Play.");
            if (world.IndexOfAny(['"', '\r', '\n', '\0']) >= 0) throw new ArgumentException("Invalid world directory.");
            yield return "--quickPlaySingleplayer";
            yield return StringHelper.FixArgument(world);
        }
    }
}
