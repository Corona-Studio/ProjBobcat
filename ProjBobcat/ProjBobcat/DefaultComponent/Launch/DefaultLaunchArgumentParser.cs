using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    public class DefaultLaunchArgumentParser : LaunchArgumentParserBase, IArgumentParser
    {
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
            IVersionLocator versionLocator, AuthResult authResult, string rootPath, string rootVersion)
        {
            if (launchSettings == null || launcherProfileParser == null)
                throw new ArgumentNullException();

            AuthResult = authResult;
            VersionLocator = versionLocator;
            RootPath = rootPath;
            LaunchSettings = launchSettings;
            LauncherProfileParser = launcherProfileParser;
            VersionInfo = LaunchSettings.VersionLocator.GetGame(LaunchSettings.Version);
            GameProfile = LauncherProfileParser.GetGameProfile(LaunchSettings.GameName);

            ClassPath = string.Join(string.Empty,
                VersionInfo.Libraries.Select(l =>
                    $"{GamePathHelper.GetLibraryPath(launchSettings.GameResourcePath, l.Path.Replace('/', '\\'))};"));
            ClassPath += string.IsNullOrEmpty(rootVersion)
                ? GamePathHelper.GetGameExecutablePath(rootPath, launchSettings.Version)
                : GamePathHelper.GetGameExecutablePath(rootPath, rootVersion);
            LastAuthResult = LaunchSettings.Authenticator.GetLastAuthResult();
        }

        /// <summary>
        ///     解析JVM核心启动参数（内存大小、Agent等）
        /// </summary>
        /// <returns></returns>
        public string ParseJvmHeadArguments()
        {
            var sb = new StringBuilder();

            if (LaunchSettings == null ||
                LaunchSettings.GameArguments == null && LaunchSettings.FallBackGameArguments == null)
                throw new ArgumentNullException("重要参数为Null!");

            if (!string.IsNullOrEmpty(LaunchSettings.GameArguments?.AgentPath))
            {
                sb.Append("-javaagent:").Append("\"").Append(LaunchSettings.GameArguments.AgentPath).Append("\"");
                if (!string.IsNullOrEmpty(LaunchSettings.GameArguments.JavaAgentAdditionPara))
                    sb.Append("=").Append(LaunchSettings.GameArguments.JavaAgentAdditionPara);

                sb.Append(" ");
            }

            if (!string.IsNullOrEmpty(LaunchSettings.FallBackGameArguments.AgentPath))
            {
                sb.Append("-javaagent:").Append("\"").Append(LaunchSettings.FallBackGameArguments.AgentPath)
                    .Append("\"");
                if (!string.IsNullOrEmpty(LaunchSettings.FallBackGameArguments.JavaAgentAdditionPara))
                    sb.Append("=").Append(LaunchSettings.FallBackGameArguments.JavaAgentAdditionPara);

                sb.Append(" ");
            }


            if (string.IsNullOrEmpty(GameProfile?.JavaArgs))
            {
                if (LaunchSettings.GameArguments?.MaxMemory > 0)
                {
                    if (LaunchSettings.GameArguments.MinMemory < LaunchSettings.GameArguments.MaxMemory)
                    {
                        sb.Append($"-Xms{LaunchSettings.GameArguments.MinMemory}m").Append(" ");
                        sb.Append($"-Xmx{LaunchSettings.GameArguments.MaxMemory}m").Append(" ");
                    }
                    else
                    {
                        sb.Append("-Xmx2G").Append(" ");
                    }
                }
                else
                {
                    sb.Append($"-Xms{LaunchSettings.FallBackGameArguments.MinMemory}m").Append(" ");
                    sb.Append($"-Xmx{LaunchSettings.FallBackGameArguments.MaxMemory}m").Append(" ");
                }


                if (LaunchSettings.GameArguments?.GcType == GcType.Disable)
                    return sb.ToString();

                var gcArg = LaunchSettings.GameArguments?.GcType switch
                {
                    GcType.CmsGc => "-XX:+UseConcMarkSweepGC",
                    GcType.G1Gc => "-XX:+UseG1GC",
                    GcType.ParallelGc => "-XX:+UseParallelGC",
                    GcType.SerialGc => "-XX:+UseSerialGC",
                    _ => "-XX:+UseG1GC"
                };

                sb.Append(gcArg).Append(" ");
            }
            else
            {
                sb.Append(GameProfile.JavaArgs).Append(" ");
            }

            sb.Append(
                    "-XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M")
                .Append(" ");

            return sb.ToString();
        }

        /// <summary>
        ///     解析游戏JVM参数
        /// </summary>
        /// <returns></returns>
        public string ParseJvmArguments()
        {
            var jvmArgumentsDic = new Dictionary<string, string>
            {
                {"${natives_directory}", $"\"{NativeRoot}\""},
                {"${launcher_name}", $"\"{LaunchSettings.LauncherName}\""},
                {"${launcher_version}", "21"},
                {"${classpath}", $"\"{ClassPath}\""}
            };

            if (!string.IsNullOrWhiteSpace(VersionInfo.JvmArguments))
                return StringHelper.ReplaceByDic(VersionInfo.JvmArguments, jvmArgumentsDic);

            const string preset =
                "[{rules: [{action: \"allow\",os:{name: \"osx\"}}],value: [\"-XstartOnFirstThread\"]},{rules: [{action: \"allow\",os:{name: \"windows\"}}],value: \"-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump\"},{rules: [{action: \"allow\",os:{name: \"windows\",version: \"^10\\\\.\"}}],value: [\"-Dos.name=Windows 10\",\"-Dos.version=10.0\"]},\"-Djava.library.path=${natives_directory}\",\"-Dminecraft.launcher.brand=${launcher_name}\",\"-Dminecraft.launcher.version=${launcher_version}\",\"-cp\",\"${classpath}\"]";
            var preJvmArguments = VersionLocator.ParseJvmArguments(JsonConvert.DeserializeObject<List<object>>(preset));
            return StringHelper.ReplaceByDic(preJvmArguments, jvmArgumentsDic);
        }

        /// <summary>
        ///     解析游戏参数
        /// </summary>
        /// <param name="authResult"></param>
        /// <returns></returns>
        public string ParseGameArguments(AuthResult authResult)
        {
            var mcArgumentsDic = new Dictionary<string, string>
            {
                {"${version_name}", LaunchSettings.Version},
                {"${version_type}", GameProfile?.Type ?? $"\"{LaunchSettings.LauncherName}\""},
                {"${assets_root}", $"\"{AssetRoot}\""},
                {"${assets_index_name}", $"\"{VersionInfo.AssetInfo.Id}\""},
                {
                    "${game_directory}",
                    $"\"{(string.IsNullOrWhiteSpace(LaunchSettings.GamePath) ? "/" : LaunchSettings.GamePath)}\""
                },
                {"${auth_player_name}", authResult?.SelectedProfile?.Name},
                {"${auth_uuid}", authResult?.SelectedProfile?.Id},
                {"${auth_access_token}", authResult?.AccessToken},
                {"${user_properties}", authResult?.User?.Properties.ResolveUserProperties()},
                {"${user_type}", "Mojang"} // use default value as placeholder
            };

            return StringHelper.ReplaceByDic(VersionInfo.GameArguments, mcArgumentsDic);
        }

        /// <summary>
        ///     解析部分总成
        /// </summary>
        /// <returns></returns>
        public List<string> GenerateLaunchArguments()
        {
            var arguments = new List<string>
            {
                (string.IsNullOrEmpty(GameProfile?.JavaDir)
                    ? LaunchSettings.FallBackGameArguments?.JavaExecutable
                    : GameProfile.JavaDir)?.Trim(),
                ParseJvmHeadArguments().Trim(),
                ParseJvmArguments().Trim(),
                VersionInfo.MainClass,
                ParseGameArguments(AuthResult).Trim(),
                ParseAdditionalArguments().Trim()
            };

            return arguments;
        }

        /// <summary>
        ///     解析额外参数（分辨率，服务器地址）
        /// </summary>
        /// <returns></returns>
        public string ParseAdditionalArguments()
        {
            var sb = new StringBuilder();

            if (!VersionInfo.AvailableGameArguments.Any()) return sb.ToString();
            if (!VersionInfo.AvailableGameArguments.ContainsKey("has_custom_resolution")) return sb.ToString();

            if ((LaunchSettings.GameArguments.Resolution?.Height ?? 0) > 0 &&
                (LaunchSettings.GameArguments.Resolution?.Width ?? 0) > 0)
            {
                sb.Append($"--width {GameProfile.Resolution?.Width ?? LaunchSettings.GameArguments.Resolution.Width}")
                    .Append(" ");
                sb.Append(
                        $"--height {GameProfile.Resolution?.Height ?? LaunchSettings.GameArguments.Resolution.Height}")
                    .Append(" ");
            }
            else
            {
                sb.Append(
                        $"--width {GameProfile.Resolution?.Width ?? LaunchSettings.FallBackGameArguments.Resolution.Width}")
                    .Append(" ");
                sb.Append(
                        $"--height {GameProfile.Resolution?.Height ?? LaunchSettings.FallBackGameArguments.Resolution.Height}")
                    .Append(" ");
            }

            if (LaunchSettings.GameArguments.ServerSettings == null) return sb.ToString();
            sb.Append($"--server {LaunchSettings.GameArguments.ServerSettings.Address}");
            sb.Append($"--port {LaunchSettings.GameArguments.ServerSettings.Port}");

            return sb.ToString();
        }
    }
}