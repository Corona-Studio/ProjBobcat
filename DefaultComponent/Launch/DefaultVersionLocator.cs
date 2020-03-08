using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.Launch
{
    public sealed class DefaultVersionLocator : VersionLocatorBase, IVersionLocator
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

        public ILauncherProfileParser LauncherProfileParser { get; set; }

        /// <summary>
        ///     获取所有能够正常被解析的游戏信息。
        ///     Fetch all the game versions' information in the .minecraft/ folder.
        /// </summary>
        /// <returns>一个表，包含.minecraft文件夹中所有版本的所有信息。A list, containing all information of all games in .minecraft/ .</returns>
        public IEnumerable<VersionInfo> GetAllGames()
        {
            // 把每个DirectoryInfo类映射到VersionInfo类。
            // Map each DirectoryInfo dir to VersionInfo class.
            return new DirectoryInfo(GamePathHelper.GetVersionPath(RootPath)).EnumerateDirectories()
                .Select(dir => ToVersion(dir.Name)).Where(ver => ver != null);
        }

        /// <summary>
        ///     获取某个特定ID的游戏信息。
        ///     Get the game info with specific ID.
        /// </summary>
        /// <param name="id">装有游戏版本的文件夹名。The game version folder's name.</param>
        /// <returns></returns>
        public VersionInfo GetGame(string id)
        {
            var version = ToVersion(id);
            return version;
        }

        /// <summary>
        ///     解析游戏JVM参数
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public string ParseJvmArguments(List<object> arguments)
        {
            if (!(arguments?.Any() ?? false))
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var jvmRule in arguments)
            {
                if (!(jvmRule is JObject))
                {
                    sb.Append($" {jvmRule}");
                    continue;
                }

                var jvmRuleObj = (JObject) jvmRule;
                var flag = true;
                if (jvmRuleObj.ContainsKey("rules"))
                    foreach (var rule in jvmRuleObj["rules"].Select(r => r.ToObject<JvmRules>()))
                    {
                        if (rule.OperatingSystem.ContainsKey("arch"))
                        {
                            flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["arch"].Equals($"x{SystemInfoHelper.GetSystemArch()}",
                                       StringComparison.Ordinal);
                            break;
                        }

                        if (!rule.OperatingSystem.ContainsKey("version"))
                            flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["name"].Equals("windows", StringComparison.Ordinal);
                        else
                            flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["name"].Equals("windows", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["version"].Equals($"^{SystemInfoHelper.GetSystemVersion()}\\.",
                                       StringComparison.Ordinal);
                    }

                if (!flag) continue;

                if (!jvmRuleObj.ContainsKey("value")) continue;

                if (jvmRuleObj["value"].Type == JTokenType.Array)
                {
                    sb.Append(" ");
                    foreach (var arg in jvmRuleObj["value"])
                        sb.Append(" ").Append(StringHelper.FixArgument(arg.ToString()));
                }
                else
                {
                    sb.Append($" {StringHelper.FixArgument(jvmRuleObj["value"].ToString())}");
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        ///     解析游戏参数
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private protected override Tuple<string, Dictionary<string, string>> ParseGameArguments(
            Tuple<string, List<object>> arguments)
        {
            var sb = new StringBuilder();
            var availableArguments = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(arguments.Item1))
            {
                sb.Append(arguments.Item1);
                return new Tuple<string, Dictionary<string, string>>(sb.ToString(), availableArguments);
            }

            if (!(arguments.Item2?.Any() ?? false))
                return new Tuple<string, Dictionary<string, string>>(sb.ToString(), availableArguments);

            foreach (var gameRule in arguments.Item2)
            {
                if (!(gameRule is JObject))
                {
                    sb.Append($" {gameRule}");
                    continue;
                }

                var gameRuleObj = (JObject) gameRule;

                if (!gameRuleObj.ContainsKey("rules")) continue;

                var ruleKey = string.Empty;
                var ruleValue = string.Empty;

                foreach (var rule in gameRuleObj["rules"].Select(r => r.ToObject<GameRules>()))
                {
                    if (!rule.Action.Equals("allow", StringComparison.Ordinal)) continue;
                    if (!rule.Features.Any()) continue;
                    if (!rule.Features.First().Value) continue;

                    ruleKey = rule.Features.First().Key;

                    if (!gameRuleObj.ContainsKey("value")) continue;
                    ruleValue = gameRuleObj["value"].Type == JTokenType.String
                        ? gameRuleObj["value"].ToString()
                        : string.Join(" ", gameRuleObj["value"]);
                }

                if (!string.IsNullOrEmpty(ruleValue)) availableArguments.Add(ruleKey, ruleValue);
            }

            return new Tuple<string, Dictionary<string, string>>(sb.ToString().Trim(), availableArguments);
            ;
        }

        /// <summary>
        ///     获取Natives与Libraries（核心依赖）列表
        ///     Fetch list of Natives & Libraries.
        /// </summary>
        /// <param name="libraries">反序列化后的库数据。Deserialized library data.</param>
        /// <returns>二元组（包含一组list，T1是Natives列表，T2是Libraries列表）。A tuple.(T1 -> Natives, T2 -> Libraries)</returns>
        private protected override Tuple<List<NativeFileInfo>, List<FileInfo>> GetNatives(
            IEnumerable<Library> libraries)
        {
            var result =
                new Tuple<List<NativeFileInfo>, List<FileInfo>>(new List<NativeFileInfo>(), new List<FileInfo>());

            // 扫描库数据。
            // Scan the library data.
            foreach (var lib in libraries)
            {
                if (!lib.ClientRequired) continue;
                if (!lib.Rules.CheckAllow()) continue;

                // 不同版本的Minecraft有不同的library JSON字符串的结构。
                // Different versions of Minecraft have different library JSON's structure.

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
                        result.Item2.Add(lib.Downloads.Artifact);
                    }
                }
                else
                {
                    if (!(lib.Natives?.Any() ?? false))
                    {
                        result.Item2.Add(new FileInfo
                        {
                            Name = lib.Name
                        });
                        continue;
                    }
                }

                if (!(lib.Natives?.Any() ?? false)) continue;
                if (lib.Downloads.Classifiers == null) continue;

                var key = lib.Natives.ContainsKey("windows")
                    ? lib.Natives["windows"].Replace("${arch}", SystemInfoHelper.GetSystemArch())
                    : "natives-windows";

                if (lib.Downloads.Classifiers.ContainsKey(key)) lib.Downloads.Classifiers[key].Name = lib.Name;

                result.Item1.Add(new NativeFileInfo
                {
                    Extract = lib.Extract,
                    FileInfo = lib.Downloads.Classifiers[key]
                });
            }

            return result;
        }

        /// <summary>
        ///     反序列化基础游戏JSON信息，以供解析器处理。
        ///     Deserialize basic JSON data of the game to make the data processable for the analyzer.
        /// </summary>
        /// <param name="id">游戏文件夹名。Name of the game's folder.</param>
        /// <returns></returns>
        private protected override RawVersionModel ParseRawVersion(string id)
        {
            // 预防I/O的错误。
            // Prevents errors related to I/O.
            if (!Directory.Exists(GamePathHelper.GetGamePath(RootPath, id)))
                return null;
            if (!File.Exists(GamePathHelper.GetGameJsonPath(RootPath, id)))
                return null;

            var versionJson =
                JsonConvert.DeserializeObject<RawVersionModel>(
                    File.ReadAllText(GamePathHelper.GetGameJsonPath(RootPath, id)));

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
        private protected override VersionInfo ToVersion(string id)
        {
            // 反序列化。
            // Deserialize.
            var rawVersion = ParseRawVersion(id);
            if (rawVersion == null)
                return null;

            List<RawVersionModel> inherits = null;
            // 检查游戏是否存在继承关系。
            // Check if there is inheritance.
            if (!string.IsNullOrEmpty(rawVersion.InheritsFrom))
            {
                // 存在继承关系。
                // Inheritance exists.

                inherits = new List<RawVersionModel>();
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

            // 生成一个随机的名字来防止重复。
            // Generates a random name to avoid duplication.
            var rs = new RandomStringHelper().UseLower().UseUpper().UseNumbers().HardMix(1);
            var randomName =
                $"{id}-{rs.Generate(5)}-{rs.Generate(5)}";

            var result = new VersionInfo
            {
                AssetInfo = rawVersion.AssetIndex,
                MainClass = rawVersion.MainClass,
                Libraries = new List<FileInfo>(),
                Natives = new List<NativeFileInfo>(),
                Id = rawVersion.Id,
                Name = randomName
            };

            // 检查游戏是否存在继承关系。
            // Check if there is inheritance.
            if (inherits?.Any() ?? false)
            {
                // 存在继承关系。
                // Inheritance exists.

                var flag = true;
                var jvmSb = new StringBuilder();
                var gameArgsSb = new StringBuilder();

                result.RootVersion = inherits.Last().Id;

                // 遍历所有的继承
                // Go through all inherits
                for (var i = inherits.Count - 1; i >= 0; i--)
                {
                    if (result.AssetInfo == null && inherits[i].AssetIndex != null)
                        result.AssetInfo = inherits[i].AssetIndex;

                    if (flag)
                    {
                        var rootLibs = GetNatives(inherits[i].Libraries);

                        result.Libraries = rootLibs.Item2;
                        result.Natives = rootLibs.Item1;

                        jvmSb.Append(ParseJvmArguments(inherits[i].Arguments?.Jvm));

                        var rootArgs = ParseGameArguments(
                            new Tuple<string, List<object>>(inherits[i].MinecraftArguments,
                                inherits[i].Arguments?.Game));
                        gameArgsSb.Append(rootArgs.Item1);
                        result.AvailableGameArguments = rootArgs.Item2;

                        flag = false;
                        continue;
                    }

                    var middleLibs = GetNatives(inherits[i].Libraries);

                    foreach (var mL in middleLibs.Item2)
                    {
                        var mLMaven = mL.Name.ResolveMavenString();
                        var mLFlag = false;
                        for (var j = 0; j < result.Libraries.Count; j++)
                        {
                            var lMaven = result.Libraries[j].Name.ResolveMavenString();
                            if (!lMaven.GetMavenFullName().Equals(mLMaven.GetMavenFullName(), StringComparison.Ordinal))
                                continue;

                            var v1 = new Version(lMaven.Version);
                            var v2 = new Version(mLMaven.Version);

                            if (v2 > v1)
                                result.Libraries[j] = mL;

                            mLFlag = true;
                        }

                        if (mLFlag)
                            continue;

                        result.Libraries.Add(mL);
                    }

                    var currentNativesNames = new List<string>();
                    result.Natives.ForEach(n => { currentNativesNames.Add(n.FileInfo.Name); });
                    var moreMiddleNatives =
                        middleLibs.Item1.Where(mL => !currentNativesNames.Contains(mL.FileInfo.Name)).ToList();
                    result.Natives.AddRange(moreMiddleNatives);


                    var jvmArgs = ParseJvmArguments(inherits[i].Arguments?.Jvm);
                    var middleGameArgs = ParseGameArguments(
                        new Tuple<string, List<object>>(inherits[i].MinecraftArguments, inherits[i].Arguments?.Game));

                    if (string.IsNullOrEmpty(inherits[i].MinecraftArguments))
                    {
                        jvmSb.Append(" ").Append(jvmArgs);
                        gameArgsSb.Append(" ").Append(middleGameArgs.Item1);
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

                    result.Id = inherits[i].Id ?? result.Id;
                    result.MainClass = inherits[i].MainClass ?? result.MainClass;
                }

                var finalJvmArgs = (result.JvmArguments ?? string.Empty).Split(' ').ToList();
                finalJvmArgs.AddRange(jvmSb.ToString().Split(' '));
                result.JvmArguments = string.Join(" ", finalJvmArgs.Distinct());

                var finalGameArgs = (result.GameArguments ?? string.Empty).Split(' ').ToList();
                finalGameArgs.AddRange(gameArgsSb.ToString().Split(' '));
                result.GameArguments = string.Join(" ", finalGameArgs.Distinct());

                goto ProcessProfile;
            }

            var libs = GetNatives(rawVersion.Libraries);
            result.Libraries = libs.Item2;
            result.Natives = libs.Item1;

            result.JvmArguments = ParseJvmArguments(rawVersion.Arguments?.Jvm);

            var gameArgs =
                ParseGameArguments(new Tuple<string, List<object>>(rawVersion.MinecraftArguments,
                    rawVersion.Arguments?.Game));
            result.GameArguments = gameArgs.Item1;
            result.AvailableGameArguments = gameArgs.Item2;

            ProcessProfile:
            var oldProfile = LauncherProfileParser.LauncherProfile.Profiles.FirstOrDefault(p =>
                p.Value.LastVersionId?.Equals(id, StringComparison.Ordinal) ?? true);
            if (oldProfile.Equals(default(KeyValuePair<string, GameProfileModel>)))
            {
                LauncherProfileParser.LauncherProfile.Profiles.Add(randomName.ToGuid().ToString("N"),
                    new GameProfileModel
                    {
                        GameDir = GamePathHelper.GetGamePath(RootPath, id),
                        LastVersionId = id,
                        Name = randomName,
                        Created = DateTime.Now
                    });
                LauncherProfileParser.SaveProfile();
                return result;
            }

            result.Name = oldProfile.Value.Name;
            oldProfile.Value.GameDir = GamePathHelper.GetGamePath(RootPath, id);
            oldProfile.Value.LastVersionId = id;
            LauncherProfileParser.LauncherProfile.Profiles[oldProfile.Key] = oldProfile.Value;
            LauncherProfileParser.SaveProfile();

            return result;
        }
    }
}