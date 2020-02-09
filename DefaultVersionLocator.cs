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

namespace ProjBobcat
{
    public sealed class DefaultVersionLocator : VersionLocatorBase, IVersionLocator
    {
        public ILauncherProfileParser LauncherProfileParser { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="clientToken"></param>
        public DefaultVersionLocator(string rootPath, Guid clientToken)
        {
            RootPath = rootPath;
            LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken);

            if (!Directory.Exists(GamePathHelper.GetVersionPath(RootPath)))
                Directory.CreateDirectory(GamePathHelper.GetVersionPath(RootPath));
        }

        /// <summary>
        /// 获取所有能够正常被解析的游戏信息
        /// </summary>
        /// <returns></returns>
        public IEnumerable<VersionInfo> GetAllGames()
        {
            return new DirectoryInfo(GamePathHelper.GetVersionPath(RootPath)).EnumerateDirectories()
                .Select(dir => ToVersion(dir.Name)).Where(ver => ver != null);
        }

        /// <summary>
        /// 获取某个特定ID的游戏信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public VersionInfo GetGame(string id)
        {
            var version = ToVersion(id);

            return version;
        }

        /// <summary>
        /// 解析游戏参数
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private protected override Tuple<string, Dictionary<string, string>> ParseGameArguments(Tuple<string, List<object>> arguments)
        {
            var sb = new StringBuilder();
            var availableArguments = new Dictionary<string, string>();

            var (mcArguments, newMcArguments) = arguments;
            if (!string.IsNullOrEmpty(mcArguments))
            {
                sb.Append(mcArguments);
                return new Tuple<string, Dictionary<string, string>>(sb.ToString(), availableArguments);
            }

            if (!(newMcArguments?.Any() ?? false))
                return new Tuple<string, Dictionary<string, string>>(sb.ToString(), availableArguments);

            foreach (var gameRule in newMcArguments)
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
                    if(!rule.Features.Any()) continue;
                    if(!rule.Features.First().Value) continue;

                    ruleKey = rule.Features.First().Key;

                    if(!gameRuleObj.ContainsKey("value")) continue;
                    ruleValue = gameRuleObj["value"].Type == JTokenType.String
                        ? gameRuleObj["value"].ToString()
                        : string.Join(" ", gameRuleObj["value"]);
                }

                if (!string.IsNullOrEmpty(ruleValue))
                {
                    availableArguments.Add(ruleKey, ruleValue);
                }
            }

            return new Tuple<string, Dictionary<string, string>>(sb.ToString().Trim(), availableArguments); ;
        }

        /// <summary>
        /// 解析游戏JVM参数
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
                {
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
                        {
                            flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["name"].Equals("windows", StringComparison.Ordinal);
                        }
                        else
                        {
                            flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["name"].Equals("windows", StringComparison.Ordinal) &&
                                   rule.OperatingSystem["version"].Equals($"^{SystemInfoHelper.GetSystemVersion()}\\.",
                                       StringComparison.Ordinal);
                        }
                    }
                }

                if (!flag) continue;

                if (!jvmRuleObj.ContainsKey("value")) continue;

                if (jvmRuleObj["value"].Type == JTokenType.Array)
                {
                    sb.Append(" "); 
                    foreach (var arg in jvmRuleObj["value"])
                    {
                        sb.Append(" ").Append(StringHelper.FixArgument(arg.ToString()));
                    }
                }
                else
                {
                    sb.Append($" {StringHelper.FixArgument(jvmRuleObj["value"].ToString())}");
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// 获取Natives与Libraries（核心依赖）列表
        /// </summary>
        /// <param name="libraries"></param>
        /// <returns>二元组（包含一组list，T1是Natives列表，T2是Libraries列表）</returns>
        private protected override Tuple<List<NativeFileInfo>, List<FileInfo>> GetNatives(IEnumerable<Library> libraries)
        {
            var result = new Tuple<List<NativeFileInfo>, List<FileInfo>>(new List<NativeFileInfo>(), new List<FileInfo>());

            foreach (var lib in libraries)
            {
                if(!lib.ClientRequired) continue;
                if (!lib.Rules.CheckAllow()) continue;

                if (lib.Downloads == null)
                {
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

                if (lib.Downloads.Classifiers.ContainsKey(key))
                {
                    lib.Downloads.Classifiers[key].Name = lib.Name;
                }

                result.Item1.Add(new NativeFileInfo
                {
                    Extract = lib.Extract,
                    FileInfo = lib.Downloads.Classifiers[key]
                });
            }

            return result;
        }

        /// <summary>
        /// 解析基础游戏JSON信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private protected override RawVersionModel ParseRawVersion(string id)
        {
            if (!Directory.Exists(GamePathHelper.GetGamePath(RootPath, id)))
                return null;
            if (!File.Exists(GamePathHelper.GetGameJsonPath(RootPath, id)))
                return null;

            var versionJson = JsonConvert.DeserializeObject<RawVersionModel>(File.ReadAllText(GamePathHelper.GetGameJsonPath(RootPath, id)));

            if (string.IsNullOrEmpty(versionJson.MainClass))
                return null;
            if (string.IsNullOrEmpty(versionJson.MinecraftArguments) && versionJson.Arguments == null)
                return null;

            return versionJson;
        }

        /// <summary>
        /// 游戏信息解析总成（内部方法）
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private protected override VersionInfo ToVersion(string id)
        {
            var rawVersion = ParseRawVersion(id);

            if (rawVersion == null)
                return null;

            List<RawVersionModel> inherits = null;
            if (!string.IsNullOrEmpty(rawVersion.InheritsFrom))
            {
                inherits = new List<RawVersionModel>();

                var current = rawVersion;
                var first = true;
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

                if (!inherits.Any() || inherits.Contains(null))
                {
                    return null;
                }
            }

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

            if (inherits?.Any() ?? false)
            {
                var flag = true;
                var jvmSb = new StringBuilder();
                var gameArgsSb = new StringBuilder();

                result.RootVersion = inherits.Last().Id;

                for (var i = inherits.Count - 1; i >= 0; i--)
                {
                    if (result.AssetInfo == null)
                        if (inherits[i].AssetIndex != null)
                            result.AssetInfo = inherits[i].AssetIndex;

                    if (flag)
                    {
                        var (rootNatives, rootLibraries) = GetNatives(inherits[i].Libraries);

                        result.Libraries = rootLibraries;
                        result.Natives = rootNatives;

                        jvmSb.Append(ParseJvmArguments(inherits[i].Arguments?.Jvm));

                        var (rootGameArgument, rootAvailableGameArguments) = ParseGameArguments(
                            new Tuple<string, List<object>>(inherits[i].MinecraftArguments, inherits[i].Arguments?.Game));
                        gameArgsSb.Append(rootGameArgument);
                        result.AvailableGameArguments = rootAvailableGameArguments;

                        flag = false;
                        continue;
                    }

                    var (middleNatives, middleLibraries) = GetNatives(inherits[i].Libraries);

                    foreach (var mL in middleLibraries)
                    {
                        var mLMaven = mL.Name.ResolveMavenString();
                        var mLFlag = false;
                        for(var j = 0; j < result.Libraries.Count; j++)
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

                        if(mLFlag)
                            continue;

                        result.Libraries.Add(mL);
                    }

                    var currentNativesNames = new List<string>();
                    result.Natives.ForEach(n =>
                    {
                        currentNativesNames.Add(n.FileInfo.Name);
                    });
                    var moreMiddleNatives =
                        middleNatives.Where(mL => !currentNativesNames.Contains(mL.FileInfo.Name)).ToList();
                    result.Natives.AddRange(moreMiddleNatives);
                    

                    var jvmArgs = ParseJvmArguments(inherits[i].Arguments?.Jvm);
                    var (middleGameArgument, middleAvailableGameArguments) = ParseGameArguments(
                        new Tuple<string, List<object>>(inherits[i].MinecraftArguments, inherits[i].Arguments?.Game));

                    if (string.IsNullOrEmpty(inherits[i].MinecraftArguments))
                    {
                        jvmSb.Append(" ").Append(jvmArgs);
                        gameArgsSb.Append(" ").Append(middleGameArgument);
                        result.AvailableGameArguments = result.AvailableGameArguments
                            .Union(middleAvailableGameArguments)
                            .ToDictionary(x => x.Key, y => y.Value);
                    }
                    else
                    {
                        result.JvmArguments = jvmArgs;
                        result.GameArguments = middleGameArgument;
                        result.AvailableGameArguments = middleAvailableGameArguments;
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

            var (natives, libraries) = GetNatives(rawVersion.Libraries);
            result.Libraries = libraries;
            result.Natives = natives;

            result.JvmArguments = ParseJvmArguments(rawVersion.Arguments?.Jvm);

            var (gameArgument, availableGameArguments) =
                ParseGameArguments(new Tuple<string, List<object>>(rawVersion.MinecraftArguments,
                    rawVersion.Arguments?.Game));
            result.GameArguments = gameArgument;
            result.AvailableGameArguments = availableGameArguments;

            ProcessProfile:
            var oldProfile = LauncherProfileParser.LauncherProfile.Profiles.FirstOrDefault(p =>
                p.Value.LastVersionId?.Equals(id, StringComparison.Ordinal) ?? true);
            if (oldProfile.Equals(default(KeyValuePair<string, GameProfileModel>)))
            {
                LauncherProfileParser.LauncherProfile.Profiles.Add(randomName.ToGuid().ToString("N"), new GameProfileModel
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