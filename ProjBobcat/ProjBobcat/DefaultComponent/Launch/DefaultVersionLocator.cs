using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.SystemInfo;
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
    /// <summary>
    ///     构造函数。
    ///     Constructor.
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="clientToken"></param>
    public DefaultVersionLocator(string rootPath, Guid clientToken)
    {
        RootPath = rootPath; // .minecraft/
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken);

        //防止给定路径不存在的时候Parser遍历文件夹爆炸。
        //Prevents errors in the parser's folder traversal when the given path does not exist.
        if (!Directory.Exists(GamePathHelper.GetVersionPath(RootPath)))
            Directory.CreateDirectory(GamePathHelper.GetVersionPath(RootPath));
    }

    public override IEnumerable<VersionInfo> GetAllGames()
    {
        // 把每个DirectoryInfo类映射到VersionInfo类。
        // Map each DirectoryInfo dir to VersionInfo class.
        var di = new DirectoryInfo(GamePathHelper.GetVersionPath(RootPath));

        foreach (var dir in di.EnumerateDirectories())
        {
            var version = ToVersion(dir.Name);
            if (version == null) continue;
            yield return version;
        }
    }

    public override VersionInfo? GetGame(string id)
    {
        var version = ToVersion(id);
        return version;
    }

    public override IEnumerable<string> ParseJvmArguments(IEnumerable<JsonElement>? arguments)
    {
        if (!(arguments?.Any() ?? false))
            yield break;

        foreach (var jvmRule in arguments)
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

                    if (!(values?.Any() ?? false)) continue;

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
        (string?, IEnumerable<JsonElement>?) arguments)
    {
        var argList = new List<string>();
        var availableArguments = new Dictionary<string, string>();

        var (item1, item2) = arguments;
        if (!string.IsNullOrEmpty(item1))
        {
            argList.Add(item1);
            return (argList, availableArguments);
        }

        if (!(item2?.Any() ?? false))
            return (argList, availableArguments);

        foreach (var gameRule in item2)
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

            if (!(rulesArr?.Any() ?? false)) continue;

            foreach (var rule in rulesArr)
            {
                if (!rule.Action.Equals("allow", StringComparison.Ordinal)) continue;
                if (!rule.Features.Any()) continue;
                if (!rule.Features.First().Value) continue;

                ruleKey = rule.Features.First().Key;

                if (!gameRule.TryGetProperty("value", out var value)) continue;

                ruleValue = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Array => string.Join(' ',
                        value.Deserialize(StringContext.Default.StringArray) ?? Array.Empty<string>())
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
    public override (List<NativeFileInfo>, List<FileInfo>) GetNatives(IEnumerable<Library> libraries)
    {
        var result = (new List<NativeFileInfo>(), new List<FileInfo>());
        var isForge = libraries.Any(l => l.Name.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase));

        // 扫描库数据。
        // Scan the library data.
        foreach (var lib in libraries)
        {
            if (!lib.ClientRequired && !isForge) continue;
            if (!lib.Rules.CheckAllow()) continue;

            // 不同版本的Minecraft有不同的library JSON字符串的结构。
            // Different versions of Minecraft have different library JSON's structure.

            var isNative = lib.Natives?.Any() ?? false;
            if (isNative)
            {
                /*
                var key =
                    lib.Natives!.TryGetValue(Constants.OsSymbol, out var value)
                        ? value.Replace("${arch}", SystemArch.CurrentArch.ToString("{0}"))
                        : $"natives-{Constants.OsSymbol}";
                */

                if(!lib.Natives!.TryGetValue(Constants.OsSymbol, out var value)) continue;

                var key = value.Replace("${arch}", SystemArch.CurrentArch.ToString("{0}"));

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

                    if(mavenInfo == null) continue;

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
                var mavenInfo = lib.Name.ResolveMavenString();
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
                if (lib.Downloads.Artifact.Name == null)
                {
                    lib.Downloads.Artifact.Name = lib.Name;

                    if (!result.Item2.Any(l => l.Name!.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
                        result.Item2.Add(lib.Downloads.Artifact);
                }
            }
            else
            {
                if (!(lib.Natives?.Any() ?? false))
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
        // 预防 I/O 的错误。
        // Prevents errors related to I/O.
        if (!Directory.Exists(Path.Combine(RootPath, GamePathHelper.GetGamePath(id))))
            return null;
        if (!File.Exists(GamePathHelper.GetGameJsonPath(RootPath, id)))
            return null;

        using var fs = File.OpenRead(GamePathHelper.GetGameJsonPath(RootPath, id));
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
        var rawVersion = ParseRawVersion(id);
        if (rawVersion == null)
            return null;

        List<RawVersionModel?>? inherits = null;
        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (!string.IsNullOrEmpty(rawVersion.InheritsFrom))
        {
            // 存在继承关系。
            // Inheritance exists.

            inherits = new List<RawVersionModel?>();
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
                    current = ParseRawVersion(current.InheritsFrom);
                    inherits.Add(current);
                    continue;
                }

                inherits.Add(ParseRawVersion(current.InheritsFrom));
                current = ParseRawVersion(current.InheritsFrom);
            }

            if (inherits.Contains(null)) return null;
        }

        var result = new VersionInfo
        {
            Assets = rawVersion.AssetsVersion,
            AssetInfo = rawVersion.AssetIndex,
            MainClass = rawVersion.MainClass,
            Libraries = new List<FileInfo>(),
            Natives = new List<NativeFileInfo>(),
            Logging = rawVersion.Logging,
            Id = rawVersion.Id,
            DirName = id,
            Name = id, //randomName,
            JavaVersion = rawVersion.JavaVersion
        };

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (inherits?.Any() ?? false)
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
                    var rootLibs = GetNatives(inherits[i]!.Libraries);

                    result.Libraries = rootLibs.Item2;
                    result.Natives = rootLibs.Item1;

                    jvmArgList.AddRange(ParseJvmArguments(inherits[i]!.Arguments?.Jvm));

                    var rootArgs = ParseGameArguments((inherits[i]!.MinecraftArguments,
                        inherits[i]!.Arguments?.Game));

                    gameArgList.AddRange(rootArgs.Item1);
                    result.AvailableGameArguments = rootArgs.Item2;

                    flag = false;
                    continue;
                }

                var middleLibs = GetNatives(inherits[i]!.Libraries);

                // result.Libraries.AddRange(middleLibs.Item2);

                foreach (var mL in middleLibs.Item2)
                {
                    var mLMaven = mL.Name.ResolveMavenString();
                    var mLFlag = false;

                    for (var j = 0; j < result.Libraries.Count; j++)
                    {
                        var lMaven = result.Libraries[j].Name.ResolveMavenString();
                        if (!lMaven.GetMavenFullName().Equals(mLMaven.GetMavenFullName(), StringComparison.Ordinal))
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

                var jvmArgs = ParseJvmArguments(inherits[i]!.Arguments?.Jvm);
                var middleGameArgs = ParseGameArguments(
                    (inherits[i]!.MinecraftArguments, inherits[i]!.Arguments?.Game));

                if (string.IsNullOrEmpty(inherits[i]!.MinecraftArguments))
                {
                    jvmArgList.AddRange(jvmArgs);
                    gameArgList.AddRange(middleGameArgs.Item1);
                    result.AvailableGameArguments = result.AvailableGameArguments
                        .Union(middleGameArgs.Item2)
                        .ToDictionary(x => x.Key, y => y.Value);
                }
                else
                {
                    result.JvmArguments = jvmArgs;
                    result.GameArguments = middleGameArgs.Item1;
                    result.AvailableGameArguments = middleGameArgs.Item2;
                }

                result.Id = inherits[i]!.Id ?? result.Id;
                result.MainClass = inherits[i]!.MainClass ?? result.MainClass;
            }

            var finalJvmArgs = result.JvmArguments?.ToList() ?? new List<string>();
            finalJvmArgs.AddRange(jvmArgList);
            result.JvmArguments = finalJvmArgs;

            var finalGameArgs = result.GameArguments?.ToList() ?? new List<string>();
            finalGameArgs.AddRange(gameArgList);
            finalGameArgs = finalGameArgs.Select(arg => arg.Split(' ')).SelectMany(a => a).Distinct().ToList();
            result.GameArguments = finalGameArgs;

            goto ProcessProfile;
        }

        var libs = GetNatives(rawVersion.Libraries);
        result.Libraries = libs.Item2;
        result.Natives = libs.Item1;

        result.JvmArguments = ParseJvmArguments(rawVersion.Arguments?.Jvm);

        var gameArgs =
            ParseGameArguments((rawVersion.MinecraftArguments,
                rawVersion.Arguments?.Game));
        result.GameArguments = gameArgs.Item1;
        result.AvailableGameArguments = gameArgs.Item2;

        ProcessProfile:
        var oldProfile = LauncherProfileParser.LauncherProfile.Profiles.FirstOrDefault(p =>
            p.Value.LastVersionId?.Equals(id, StringComparison.Ordinal) ?? true);

        var gamePath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        if (oldProfile.Equals(default(KeyValuePair<string, GameProfileModel>)))
        {
            LauncherProfileParser.LauncherProfile.Profiles.Add(id.ToGuidHash().ToString("N"),
                new GameProfileModel
                {
                    GameDir = gamePath,
                    LastVersionId = id,
                    Name = id,
                    Created = DateTime.Now
                });
            LauncherProfileParser.SaveProfile();
            return result;
        }

        result.Name = oldProfile.Value.Name;
        oldProfile.Value.GameDir = gamePath;
        oldProfile.Value.LastVersionId = id;
        LauncherProfileParser.LauncherProfile.Profiles[oldProfile.Key] = oldProfile.Value;
        LauncherProfileParser.SaveProfile();

        return result;
    }
}