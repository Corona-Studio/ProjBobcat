using System;
using System.Collections.Frozen;
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
using ProjBobcat.JsonConverter;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.Launch;

/// <summary>
///     默认的版本定位器
/// </summary>
public sealed class DefaultVersionLocator : VersionLocatorBase
{
    readonly object _lock = new();

    /// <summary>
    ///     构造函数。
    ///     Constructor.
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="clientToken"></param>
    public DefaultVersionLocator(string rootPath, Guid clientToken) : base(rootPath)
    {
        this.LauncherProfileParser ??= new DefaultLauncherProfileParser(rootPath, clientToken);

        //防止给定路径不存在的时候Parser遍历文件夹爆炸。
        //Prevents errors in the parser's folder traversal when the given path does not exist.
        if (!Directory.Exists(GamePathHelper.GetVersionPath(rootPath)))
            Directory.CreateDirectory(GamePathHelper.GetVersionPath(rootPath));
    }

    public NativeReplacementPolicy NativeReplacementPolicy { get; init; } = NativeReplacementPolicy.LegacyOnly;

    public override IEnumerable<VersionInfo> GetAllGames()
    {
        // 把每个DirectoryInfo类映射到VersionInfo类。
        // Map each DirectoryInfo dir to VersionInfo class.
        var di = new DirectoryInfo(GamePathHelper.GetVersionPath(this.RootPath));

        foreach (var dir in di.EnumerateDirectories())
        {
            var version = this.ToVersion(dir.Name);
            if (version == null) continue;
            yield return version;
        }
    }

    public override VersionInfo? GetGame(string id)
    {
        var version = this.ToVersion(id);
        return version;
    }

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
    /// <param name="arguments"></param>
    /// <returns></returns>
    private protected override (IEnumerable<string>, Dictionary<string, string>) ParseGameArguments(
        (string?, JsonElement[]?) arguments)
    {
        var argList = new List<string>();
        var availableArguments = new Dictionary<string, string>();

        var (item1, item2) = arguments;
        if (!string.IsNullOrEmpty(item1))
        {
            argList.Add(item1);
            return (argList, availableArguments);
        }

        if ((item2?.Length ?? 0) == 0)
            return (argList, availableArguments);

        foreach (var gameRule in item2!)
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
    public override RawVersionModel? ParseRawVersion(string id)
    {
        var gamePath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));
        var possibleFiles = new List<string>
        {
            GamePathHelper.GetGameJsonPath(this.RootPath, id)
        };

        // 预防 I/O 的错误。
        // Prevents errors related to I/O.
        if (!Directory.Exists(gamePath))
            return null;
        if (!File.Exists(GamePathHelper.GetGameJsonPath(this.RootPath, id)))
        {
            var files = Directory
                .EnumerateFiles(gamePath, "*.json", SearchOption.TopDirectoryOnly)
                .ToArray();

            if (files.Length == 0) return null;

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
                    return null;
                if (string.IsNullOrEmpty(versionJson.MainClass))
                    return null;
                if (string.IsNullOrEmpty(versionJson.MinecraftArguments) && versionJson.Arguments == null)
                    return null;

                return versionJson;
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    /// <summary>
    ///     游戏信息解析。
    ///     Game info analysis.
    /// </summary>
    /// <param name="id">游戏文件夹名。Name of the game version's folder.</param>
    /// <returns>一个VersionInfo类。A VersionInfo class.</returns>
    private protected override VersionInfo? ToVersion(string id)
    {
        // 反序列化。
        // Deserialize.
        var rawVersion = this.ParseRawVersion(id);
        if (rawVersion == null)
            return null;

        var inherits = new List<RawVersionModel>();

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (!string.IsNullOrEmpty(rawVersion.InheritsFrom))
        {
            // 存在继承关系。
            // Inheritance exists.

            var current = rawVersion;
            var first = true;

            // 递归式地将所有反序列化的版本继承塞进一个表中。
            // Add all deserialized inherited version to a list recursively.
            while (current != null && !string.IsNullOrEmpty(current.InheritsFrom))
            {
                if (first)
                {
                    inherits.Add(current);
                    first = false;
                    current = this.ParseRawVersion(current.InheritsFrom);

                    if (current == null) return null;

                    inherits.Add(current);
                    continue;
                }

                var inheritVersion = this.ParseRawVersion(current.InheritsFrom);

                if (inheritVersion == null) return null;

                inherits.Add(inheritVersion);
                current = this.ParseRawVersion(current.InheritsFrom);
            }
        }

        var result = new VersionInfo
        {
            Assets = rawVersion.AssetsVersion,
            AssetInfo = rawVersion.AssetIndex,
            MainClass = rawVersion.MainClass,
            Libraries = [],
            Natives = [],
            Logging = rawVersion.Logging,
            Id = rawVersion.Id,
            InheritsFrom = rawVersion.InheritsFrom,
            GameBaseVersion = GameVersionHelper.TryGetMcVersion([.. inherits, rawVersion]) ?? id,
            DirName = id,
            Name = id,
            JavaVersion = rawVersion.JavaVersion,
            GameArguments = []
        };

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (inherits is { Count: > 0 })
        {
            // 存在继承关系。
            // Inheritance exists.

            var flag = true;
            var jvmArgList = new List<string>();
            var gameArgList = new List<string>();

            result.RootVersion = inherits.Last()!.Id;

            // 遍历所有的继承
            // Go through all inherits
            for (var i = inherits.Count - 1; i >= 0; i--)
            {
                if (result.JavaVersion == null && inherits[i]!.JavaVersion != null)
                    result.JavaVersion = inherits[i]!.JavaVersion;
                if (result.AssetInfo == null && inherits[i]!.AssetIndex != null)
                    result.AssetInfo = inherits[i]!.AssetIndex;

                if (flag)
                {
                    var inheritsLibs = inherits[i]!.Libraries.ToList();
                    inheritsLibs = NativeReplaceHelper.Replace([rawVersion, ..inherits!], inheritsLibs,
                        this.NativeReplacementPolicy);

                    var rootLibs = this.GetNatives([.. inheritsLibs]);
                    result.Libraries = rootLibs.Item2;
                    result.Natives = rootLibs.Item1;

                    jvmArgList.AddRange(this.ParseJvmArguments(inherits[i]!.Arguments?.Jvm));

                    var rootArgs = this.ParseGameArguments(
                        (inherits[i]!.MinecraftArguments, inherits[i]!.Arguments?.Game));

                    gameArgList.AddRange(rootArgs.Item1);
                    result.AvailableGameArguments = rootArgs.Item2;

                    flag = false;
                    continue;
                }

                var middleLibs = this.GetNatives(inherits[i]!.Libraries);

                // result.Libraries.AddRange(middleLibs.Item2);

                foreach (var mL in middleLibs.Item2)
                {
                    if (string.IsNullOrEmpty(mL.Name)) continue;

                    var mLMaven = mL.Name.ResolveMavenString()!;
                    var mLFlag = false;

                    for (var j = 0; j < result.Libraries.Count; j++)
                    {
                        if (string.IsNullOrEmpty(result.Libraries[j].Name))
                            continue;

                        var lMaven = result.Libraries[j].Name!.ResolveMavenString()!;

                        if (!lMaven.GetMavenFullName()
                                .Equals(mLMaven.GetMavenFullName(), StringComparison.OrdinalIgnoreCase))
                            continue;

                        var v1 = new ComparableVersion(lMaven.Version);
                        var v2 = new ComparableVersion(mLMaven.Version);

                        if (v2 > v1)
                            result.Libraries[j] = mL;

                        mLFlag = true;
                    }

                    if (mLFlag)
                        continue;

                    result.Libraries.Add(mL);
                }

                var currentNativesNames = new List<string>(result.Natives
                    .Where(mL => !string.IsNullOrEmpty(mL.FileInfo.Name))
                    .Select(mL => mL.FileInfo.Name!));
                var moreMiddleNatives =
                    middleLibs.Item1
                        .Where(mL => !string.IsNullOrEmpty(mL.FileInfo.Name))
                        .Where(mL => !currentNativesNames.Contains(mL.FileInfo.Name!))
                        .ToList();
                result.Natives.AddRange(moreMiddleNatives);

                var jvmArgs = this.ParseJvmArguments(inherits[i]!.Arguments?.Jvm);
                var middleGameArgs = this.ParseGameArguments(
                    (inherits[i]!.MinecraftArguments, inherits[i]!.Arguments?.Game));

                if (string.IsNullOrEmpty(inherits[i]!.MinecraftArguments))
                {
                    jvmArgList.AddRange(jvmArgs);
                    gameArgList.AddRange(middleGameArgs.Item1);
                    result.AvailableGameArguments = result.AvailableGameArguments?
                        .Union(middleGameArgs.Item2)
                        .ToDictionary(x => x.Key, y => y.Value);
                }
                else
                {
                    result.JvmArguments = jvmArgs.ToList();
                    result.GameArguments = middleGameArgs.Item1;
                    result.AvailableGameArguments = middleGameArgs.Item2;
                }

                result.Id = inherits[i]?.Id ?? result.Id;
                result.MainClass = inherits[i]?.MainClass ?? result.MainClass;
            }

            if (result.JvmArguments != null)
                jvmArgList.AddRange(result.JvmArguments);

            result.JvmArguments = jvmArgList;

            if (result.GameArguments != null)
                gameArgList.AddRange(result.GameArguments);

            result.GameArguments = gameArgList
                .Select(arg => arg.Split(' '))
                .SelectMany(a => a)
                .ToFrozenSet();

            this.ProcessProfile(result, id);

            return result;
        }

        var rawLibs = rawVersion.Libraries.ToList();
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

            for (var i = 1; i < sortedDuplicates.Count; i++) rawLibs.Remove(sortedDuplicates[i]);
        }

        rawLibs = NativeReplaceHelper.Replace([rawVersion, .. inherits ?? []], rawLibs, this.NativeReplacementPolicy);

        var libs = this.GetNatives([.. rawLibs]);

        result.Libraries = libs.Item2;
        result.Natives = libs.Item1;
        result.JvmArguments = this.ParseJvmArguments(rawVersion.Arguments?.Jvm).ToList();

        var gameArgs = this.ParseGameArguments((rawVersion.MinecraftArguments,
            rawVersion.Arguments?.Game));
        result.GameArguments = gameArgs.Item1;
        result.AvailableGameArguments = gameArgs.Item2;

        this.ProcessProfile(result, id);

        return result;
    }

    void ProcessProfile(VersionInfo result, string id)
    {
        if (this.LauncherProfileParser == null) return;

        var gameId = id.ToGuidHash().ToString("N");
        var gamePath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));

        lock (this._lock)
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