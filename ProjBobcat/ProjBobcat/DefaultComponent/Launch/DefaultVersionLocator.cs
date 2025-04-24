using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.NativeReplace;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.Version;
using ProjBobcat.Interface;
using ProjBobcat.JsonConverter;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

#if NET9_0_OR_GREATER
using System.Threading;
#endif

namespace ProjBobcat.DefaultComponent.Launch;

/// <summary>
///     默认的版本定位器
/// </summary>
public sealed class DefaultVersionLocator : VersionLocatorBase
{
#if NET9_0_OR_GREATER
    readonly Lock _lock = new();
#else
    readonly object _lock = new();
#endif

    private readonly string _rootPath;

    /// <summary>
    ///     构造函数。
    ///     Constructor.
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="clientToken"></param>
    public DefaultVersionLocator(string rootPath, Guid clientToken)
    {
        _rootPath = rootPath;
        this.LauncherProfileParser ??= new DefaultLauncherProfileParser(rootPath, clientToken);

        //防止给定路径不存在的时候Parser遍历文件夹爆炸。
        //Prevents errors in the parser's folder traversal when the given path does not exist.
        if (!Directory.Exists(GamePathHelper.GetVersionPath(rootPath)))
            Directory.CreateDirectory(GamePathHelper.GetVersionPath(rootPath));
    }

    public override IEnumerable<IVersionInfo> GetAllGames()
    {
        // 把每个DirectoryInfo类映射到VersionInfo类。
        // Map each DirectoryInfo dir to VersionInfo class.
        var di = new DirectoryInfo(GamePathHelper.GetVersionPath(_rootPath));
        
        foreach (var dir in di.EnumerateDirectories())
        {
            var version = this.ToVersion(dir.Name);

            yield return version;
        }
    }

    public override IVersionInfo GetGame(string id) => this.ToVersion(id);

    public override IEnumerable<string> ParseJvmArguments(JsonElement[]? arguments)
    {
        if ((arguments?.Length ?? 0) == 0)
            yield break;

        foreach (var jvmRule in arguments!)
        {
            if (jvmRule.ValueKind == JsonValueKind.String)
            {
                var str = jvmRule.GetString();

                if (!string.IsNullOrEmpty(str)) yield return str;

                continue;
            }

            if (jvmRule.TryGetProperty("rules", out var rules))
                if (!(rules.Deserialize(JvmRulesContext.Default.JvmRulesArray)?.CheckAllow() ?? false))
                    continue;
            if (!jvmRule.TryGetProperty("value", out var value)) continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    var values = value.Deserialize(StringContext.Default.StringArray);

                    if (values == null || values.Length == 0) continue;

                    foreach (var val in values)
                        yield return StringHelper.FixArgument(val);

                    break;
                case JsonValueKind.String:
                    var valStr = value.GetString();

                    if (!string.IsNullOrEmpty(valStr))
                        yield return StringHelper.FixArgument(valStr);

                    break;
            }
        }
    }

    /// <summary>
    ///     解析游戏参数
    /// </summary>
    /// <returns></returns>
    private (IEnumerable<string>, Dictionary<string, string>) ParseGameArguments(string? minecraftArgs, JsonElement[]? gameArgs)
    {
        var argList = new List<string>();
        var availableArguments = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(minecraftArgs))
        {
            argList.Add(minecraftArgs);
            return (argList, availableArguments);
        }

        if ((gameArgs?.Length ?? 0) == 0)
            return (argList, availableArguments);

        foreach (var gameRule in gameArgs!)
        {
            if (gameRule.ValueKind == JsonValueKind.String)
            {
                var val = gameRule.GetString();

                if (!string.IsNullOrEmpty(val))
                    argList.Add(val);

                continue;
            }

            if (!gameRule.TryGetProperty("rules", out var rules)) continue;

            var ruleKey = string.Empty;
            var ruleValue = string.Empty;

            var rulesArr = rules.Deserialize(GameRulesContext.Default.GameRulesArray);

            if (rulesArr == null || rulesArr.Length == 0) continue;

            foreach (var rule in rulesArr)
            {
                if (!rule.Action.Equals("allow", StringComparison.Ordinal)) continue;
                if (rule.Features.Count == 0) continue;
                if (!rule.Features.First().Value) continue;

                ruleKey = rule.Features.First().Key;

                if (!gameRule.TryGetProperty("value", out var value)) continue;

                ruleValue = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Array => string.Join(' ',
                        value.Deserialize(StringContext.Default.StringArray) ?? []),
                    _ => string.Empty
                };
            }

            if (!string.IsNullOrEmpty(ruleValue)) availableArguments.Add(ruleKey, ruleValue);
        }

        return (argList, availableArguments);
    }

    /// <summary>
    ///     获取Natives与Libraries（核心依赖）列表
    ///     Fetch list of Natives and Libraries.
    /// </summary>
    /// <param name="libraries">反序列化后的库数据。Deserialized library data.</param>
    /// <returns>二元组（包含一组list，T1是Natives列表，T2是Libraries列表）。A tuple.(T1 -> Natives, T2 -> Libraries)</returns>
    public override (List<NativeFileInfo>, List<FileInfo>) GetNatives(Library[] libraries)
    {
        var result = (new List<NativeFileInfo>(), new List<FileInfo>());

        var isForge = libraries
            .Any(l => l.Name.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase));

        // 扫描库数据。
        // Scan the library data.
        foreach (var lib in libraries)
        {
            if (!lib.ClientRequired && !isForge) continue;
            if (!lib.Rules.CheckAllow()) continue;

            // 不同版本的Minecraft有不同的library JSON字符串的结构。
            // Different versions of Minecraft have different library JSON's structure.

            // Fix for new native format introduced in 1.19
            if (lib.IsNewNativeLib())
            {
                result.Item1.Add(new NativeFileInfo
                {
                    Extract = lib.Extract,
                    FileInfo = lib.Downloads!.Artifact!
                });

                continue;
            }

            var isNative = (lib.Natives?.Count ?? 0) > 0;
            if (isNative)
            {
                /*
                var key =
                    lib.Natives!.TryGetValue(Constants.OsSymbol, out var value)
                        ? value.Replace("${arch}", SystemArch.CurrentArch.ToString("{0}"))
                        : $"natives-{Constants.OsSymbol}";
                */

                if (!lib.Natives!.TryGetValue(Constants.OsSymbol, out var value)) continue;

                var key = value.Replace("${arch}", SystemInfoHelper.GetSystemArch().TrimStart('x'));

                FileInfo libFi;
                if (lib.Downloads?.Classifiers?.ContainsKey(key) ?? false)
                {
                    lib.Downloads.Classifiers[key].Name = lib.Name;
                    libFi = lib.Downloads.Classifiers[key];
                }
                else
                {
                    var libName = lib.Name;

                    if (!lib.Name.EndsWith($":{key}", StringComparison.OrdinalIgnoreCase)) libName += $":{key}";

                    var mavenInfo = libName.ResolveMavenString();

                    if (mavenInfo == null) continue;

                    var downloadUrl = string.IsNullOrEmpty(lib.Url)
                        ? mavenInfo.OrganizationName.Equals("net.minecraftforge", StringComparison.Ordinal)
                            ? "https://files.minecraftforge.net/maven/"
                            : "https://libraries.minecraft.net/"
                        : lib.Url;

                    libFi = new FileInfo
                    {
                        Name = lib.Name,
                        Url = $"{downloadUrl}{mavenInfo.Path}",
                        Path = mavenInfo.Path
                    };
                }

                result.Item1.Add(new NativeFileInfo
                {
                    Extract = lib.Extract,
                    FileInfo = libFi
                });

                continue;
            }

            if (lib.Downloads == null)
            {
                // 一些Library项不包含下载数据，所以我们直接解析Maven的名称来猜测下载链接。
                // Some library items don't contain download data, so we directly resolve maven's name to guess the download link.
                var mavenInfo = lib.Name.ResolveMavenString()!;
                var downloadUrl = string.IsNullOrEmpty(lib.Url)
                    ? mavenInfo.OrganizationName.Equals("net.minecraftforge", StringComparison.Ordinal)
                        ? "https://files.minecraftforge.net/maven/"
                        : "https://libraries.minecraft.net/"
                    : lib.Url;

                result.Item2.Add(new FileInfo
                {
                    Name = lib.Name,
                    Path = mavenInfo.Path,
                    Url = $"{downloadUrl}{mavenInfo.Path}"
                });
                continue;
            }

            if (lib.Downloads?.Artifact != null)
            {
                if (string.IsNullOrEmpty(lib.Downloads.Artifact.Name))
                    lib.Downloads.Artifact.Name = lib.Name;

                if (!result.Item2.Any(l => l.Name!.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
                    result.Item2.Add(lib.Downloads.Artifact);
            }
            else
            {
                if ((lib.Natives?.Count ?? 0) != 0) continue;
                if (!result.Item2.Any(l => l.Name!.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
                    result.Item2.Add(new FileInfo
                    {
                        Name = lib.Name
                    });
            }
        }

        return result;
    }

    /// <summary>
    ///     反序列化基础游戏JSON信息，以供解析器处理。
    ///     Deserialize basic JSON data of the game to make the data processable for the analyzer.
    /// </summary>
    /// <param name="id">游戏文件夹名。Name of the game's folder.</param>
    /// <returns></returns>
    public override (GameBrokenReason?, RawVersionModel?) ParseRawVersion(string id)
    {
        var gamePath = Path.Combine(_rootPath, GamePathHelper.GetGamePath(id));
        var possibleFiles = new List<string>
        {
            GamePathHelper.GetGameJsonPath(_rootPath, id)
        };

        // 预防 I/O 的错误。
        // Prevents errors related to I/O.
        if (!Directory.Exists(gamePath))
            return (GameBrokenReason.GamePathNotFound, null);
        
        if (!File.Exists(GamePathHelper.GetGameJsonPath(_rootPath, id)))
        {
            var files = Directory
                .EnumerateFiles(gamePath, "*.json", SearchOption.TopDirectoryOnly)
                .ToArray();

            if (files.Length == 0)
                return (GameBrokenReason.LackGameJson, null);

            possibleFiles.AddRange(files);
        }

        foreach (var possibleFile in possibleFiles)
        {
            if (!File.Exists(possibleFile)) continue;

            try
            {
                using var fs = File.OpenRead(possibleFile);
                var options = new JsonSerializerOptions
                {
                    Converters =
                    {
                        new DateTimeConverterUsingDateTimeParse()
                    }
                };
                var versionJsonObj = JsonSerializer.Deserialize(
                    fs, typeof(RawVersionModel), new RawVersionModelContext(options));

                if (versionJsonObj is not RawVersionModel versionJson)
                    continue;
                if (string.IsNullOrEmpty(versionJson.MainClass))
                    continue;
                if (string.IsNullOrEmpty(versionJson.MinecraftArguments) && versionJson.Arguments == null)
                    continue;

                return (null, versionJson);
            }
            catch (JsonException)
            {
            }
        }

        return (GameBrokenReason.NoCandidateJsonFound, null);
    }

    public override ResolvedGameVersion? ResolveGame(
        IVersionInfo rawVersionInfo,
        NativeReplacementPolicy nativeReplacementPolicy,
        JavaRuntimeInfo? javaRuntimeInfo)
    {
        if (rawVersionInfo is not VersionInfo versionInfo)
            return null;
        if (versionInfo.RawVersion == null)
            return null;

        List<RawVersionModel> inherits = [versionInfo.RawVersion, .. versionInfo.InheritsVersions ?? []];
        var libraries = new List<FileInfo>();
        var natives = new List<NativeFileInfo>();
        var availableGameArgs = new Dictionary<string, string>();
        var jvmArguments = new List<string>();
        var gameArguments = new List<string>();
        var mainClass = versionInfo.RawVersion.MainClass;
        string? assets = null;
        Asset? assetInfo = null;
        Class.Model.Logging? logging = null;

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (inherits is { Count: > 0 })
        {
            // 存在继承关系。
            // Inheritance exists.

            var flag = true;
            var jvmArgList = new List<string>();
            var gameArgList = new List<string>();

            // 遍历所有的继承
            // Go through all inherits
            for (var i = inherits.Count - 1; i >= 0; i--)
            {
                if (assets == null && inherits[i].AssetsVersion != null)
                    assets = inherits[i].AssetsVersion;
                if (assetInfo == null && inherits[i].AssetIndex != null)
                    assetInfo = inherits[i].AssetIndex;
                if (logging == null && inherits[i].Logging != null)
                    logging = inherits[i].Logging;

                if (flag)
                {
                    var inheritsLibs = inherits[i].Libraries.ToList();
                    inheritsLibs = NativeReplaceHelper.Replace(
                        [versionInfo.RawVersion, .. inherits],
                        inheritsLibs,
                        nativeReplacementPolicy,
                        javaRuntimeInfo?.JavaPlatform,
                        javaRuntimeInfo?.JavaArch,
                        javaRuntimeInfo?.UseSystemGlfwOnLinux ?? false,
                        javaRuntimeInfo?.UseSystemOpenAlOnLinux ?? false);

                    var rootLibs = this.GetNatives([.. inheritsLibs]);
                    libraries = rootLibs.Item2;
                    natives = rootLibs.Item1;

                    jvmArgList.AddRange(this.ParseJvmArguments(inherits[i].Arguments?.Jvm));

                    var rootArgs = this.ParseGameArguments(
                        inherits[i].MinecraftArguments,
                        inherits[i].Arguments?.Game);

                    gameArgList.AddRange(rootArgs.Item1);
                    availableGameArgs = rootArgs.Item2;

                    flag = false;
                    continue;
                }

                var middleLibs = this.GetNatives(inherits[i].Libraries);

                foreach (var mL in middleLibs.Item2)
                {
                    if (string.IsNullOrEmpty(mL.Name)) continue;

                    var mLMaven = mL.Name.ResolveMavenString()!;
                    var mLFlag = false;

                    for (var j = 0; j < libraries.Count; j++)
                    {
                        if (string.IsNullOrEmpty(libraries[j].Name))
                            continue;

                        var lMaven = libraries[j].Name!.ResolveMavenString()!;

                        if (!lMaven.GetMavenFullName()
                                .Equals(mLMaven.GetMavenFullName(), StringComparison.OrdinalIgnoreCase))
                            continue;

                        var v1 = new ComparableVersion(lMaven.Version);
                        var v2 = new ComparableVersion(mLMaven.Version);

                        if (v2 > v1)
                            libraries[j] = mL;

                        mLFlag = true;
                    }

                    if (mLFlag)
                        continue;

                    libraries.Add(mL);
                }

                var currentNativesNames = new List<string>(natives
                    .Where(mL => !string.IsNullOrEmpty(mL.FileInfo.Name))
                    .Select(mL => mL.FileInfo.Name!));
                var moreMiddleNatives =
                    middleLibs.Item1
                        .Where(mL => !string.IsNullOrEmpty(mL.FileInfo.Name))
                        .Where(mL => !currentNativesNames.Contains(mL.FileInfo.Name!))
                        .ToList();
                natives.AddRange(moreMiddleNatives);

                var jvmArgs = this.ParseJvmArguments(inherits[i].Arguments?.Jvm);
                var middleGameArgs = this.ParseGameArguments(
                    inherits[i].MinecraftArguments,
                    inherits[i].Arguments?.Game);

                if (string.IsNullOrEmpty(inherits[i].MinecraftArguments))
                {
                    jvmArgList.AddRange(jvmArgs);
                    gameArgList.AddRange(middleGameArgs.Item1);
                    availableGameArgs = availableGameArgs
                        .Union(middleGameArgs.Item2, KeyValuePairStringStringComparer.Default)
                        .ToDictionary(x => x.Key, y => y.Value);
                }
                else
                {
                    jvmArguments = jvmArgs.ToList();
                    gameArguments = middleGameArgs.Item1.ToList();
                    availableGameArgs = middleGameArgs.Item2;
                }

                mainClass = inherits[i].MainClass;
            }

            if (jvmArguments.Count != 0)
                jvmArgList.AddRange(jvmArguments);

            jvmArguments = jvmArgList;

            if (gameArguments.Count != 0)
                gameArgList.AddRange(gameArguments);

            gameArguments = gameArgList
                .Select(arg => arg.Split(' '))
                .SelectMany(a => a)
                .ToHashSet()
                .ToList();

            if (string.IsNullOrEmpty(mainClass))
                return null;

            return new ResolvedGameVersion(
                versionInfo.RootVersion,
                versionInfo.DirName,
                mainClass,
                assets,
                assetInfo,
                logging,
                libraries,
                natives,
                jvmArguments,
                gameArguments,
                availableGameArgs);
        }

        var rawLibs = versionInfo.RawVersion.Libraries.ToList();
        var duplicateLibs = new Dictionary<string, List<Library>>();
        foreach (var lib in rawLibs)
        {
            var maven = lib.Name.ResolveMavenString();
            var fullName = maven.GetMavenFullName();

            if (duplicateLibs.TryGetValue(fullName, out var value))
                value.Add(lib);
            else
                duplicateLibs.Add(fullName, [lib]);
        }

        var filteredLibs = duplicateLibs
            .Select(p => p.Value
                .Where(lib =>
                    !lib.IsNewNativeLib() && (lib.Natives?.Count ?? 0) == 0 && lib.Downloads?.Classifiers == null)
                .Where(lib => lib.Rules?.CheckAllow() ?? true)
                .ToList())
            .Where(libs => libs.Count > 1);

        foreach (var duplicates in filteredLibs)
        {
            var sortedDuplicates = duplicates
                .OrderByDescending(l => new ComparableVersion(l.Name.ResolveMavenString()?.Version ?? "0"))
                .ToList();

            for (var i = 1; i < sortedDuplicates.Count; i++)
                rawLibs.Remove(sortedDuplicates[i]);
        }

        // Patch for PCL and HMCL
        var duplicateOw2Lib = rawLibs
            .Where(lib => lib.Name.Contains("org.ow2.asm:asm:", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (duplicateOw2Lib.Length > 1)
        {
            // Pick the latest version of org.ow2.asm:asm
            var sortedDuplicates = duplicateOw2Lib
                .OrderByDescending(l => new ComparableVersion(l.Name.ResolveMavenString()?.Version ?? "0"))
                .Skip(1);

            foreach (var duplicates in sortedDuplicates)
                rawLibs.Remove(duplicates);
        }

        rawLibs = NativeReplaceHelper.Replace(
            [versionInfo.RawVersion],
            rawLibs,
            nativeReplacementPolicy,
            javaRuntimeInfo?.JavaPlatform,
            javaRuntimeInfo?.JavaArch,
            javaRuntimeInfo?.UseSystemGlfwOnLinux ?? false,
            javaRuntimeInfo?.UseSystemOpenAlOnLinux ?? false);

        var libs = this.GetNatives([.. rawLibs]);

        libraries = libs.Item2;
        natives = libs.Item1;
        jvmArguments = this.ParseJvmArguments(versionInfo.RawVersion.Arguments?.Jvm).ToList();

        var gameArgs = this.ParseGameArguments(
            versionInfo.RawVersion.MinecraftArguments,
            versionInfo.RawVersion.Arguments?.Game);

        gameArguments = gameArgs.Item1.ToList();
        availableGameArgs = gameArgs.Item2;

        return new ResolvedGameVersion(
            versionInfo.RootVersion,
            versionInfo.DirName,
            versionInfo.RawVersion.MainClass,
            assets,
            assetInfo,
            logging,
            libraries,
            natives,
            jvmArguments,
            gameArguments,
            availableGameArgs);
    }

    /// <summary>
    ///     游戏信息解析。
    ///     Game info analysis.
    /// </summary>
    /// <param name="id">游戏文件夹名。Name of the game version's folder.</param>
    /// <returns>一个VersionInfo类。A VersionInfo class.</returns>
    private IVersionInfo ToVersion(string id)
    {
        // 反序列化。
        // Deserialize.
        var rawVersion = this.ParseRawVersion(id);
        if (rawVersion.Item1.HasValue)
            return new BrokenVersionInfo(id)
            {
                BrokenReason = rawVersion.Item1.Value
            };

        var unwrappedRawVersion = rawVersion.Item2!;
        var inherits = new List<RawVersionModel>();

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (!string.IsNullOrEmpty(unwrappedRawVersion.InheritsFrom))
        {
            // 存在继承关系。
            // Inheritance exists.

            var current = unwrappedRawVersion;

            // 递归式地将所有反序列化的版本继承塞进一个表中。
            // Add all deserialized inherited version to a list recursively.
            while (!string.IsNullOrEmpty(current.InheritsFrom))
            {
                var parentRawVersion = this.ParseRawVersion(current.InheritsFrom);

                if (parentRawVersion.Item1.HasValue)
                    return new BrokenVersionInfo(id)
                    {
                        BrokenReason = GameBrokenReason.Parent | parentRawVersion.Item1.Value
                    };

                current = parentRawVersion.Item2!;
                inherits.Add(current);
            }
        }

        var result = new VersionInfo
        {
            Assets = unwrappedRawVersion.AssetsVersion,
            Id = unwrappedRawVersion.Id,
            InheritsFrom = unwrappedRawVersion.InheritsFrom,
            GameBaseVersion = GameVersionHelper.TryGetMcVersion([.. inherits, unwrappedRawVersion]) ?? id,
            DirName = id,
            Name = id,
            JavaVersion = unwrappedRawVersion.JavaVersion,
            RawVersion = unwrappedRawVersion,
            InheritsVersions = inherits
        };

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (inherits is { Count: > 0 })
        {
            // 存在继承关系。
            // Inheritance exists.
            result.RootVersion = inherits.Last().Id;

            // 遍历所有的继承
            // Go through all inherits
            for (var i = inherits.Count - 1; i >= 0; i--)
            {
                if (result.JavaVersion == null && inherits[i].JavaVersion != null)
                    result.JavaVersion = inherits[i].JavaVersion;
            }

            this.ProcessProfile(result, id);

            return result;
        }

        this.ProcessProfile(result, id);

        return result;
    }

    void ProcessProfile(VersionInfo result, string id)
    {
        if (this.LauncherProfileParser == null) return;

        var gameId = id.ToGuidHash().ToString("N");
        var gamePath = Path.Combine(_rootPath, GamePathHelper.GetGamePath(id));

#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            if (this.LauncherProfileParser.LauncherProfile.Profiles!.TryGetValue(gameId, out var oldProfileModel))
            {
                result.Name = oldProfileModel.Name!;
                oldProfileModel.GameDir = gamePath;
                oldProfileModel.LastVersionId = id;
                this.LauncherProfileParser.LauncherProfile.Profiles![gameId] = oldProfileModel;
                this.LauncherProfileParser.SaveProfile();

                return;
            }

            var gameProfile = new GameProfileModel
            {
                GameDir = gamePath,
                LastVersionId = id,
                Name = id,
                Created = DateTime.Now
            };

            this.LauncherProfileParser.LauncherProfile.Profiles!.Add(gameId, gameProfile);
            this.LauncherProfileParser.SaveProfile();
        }
    }
}

file class KeyValuePairStringStringComparer : IEqualityComparer<KeyValuePair<string, string>>
{
    public static KeyValuePairStringStringComparer Default => new ();

    public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
    {
        return x.Key == y.Key && x.Value == y.Value;
    }

    public int GetHashCode(KeyValuePair<string, string> obj)
    {
        return HashCode.Combine(obj.Key, obj.Value);
    }
}