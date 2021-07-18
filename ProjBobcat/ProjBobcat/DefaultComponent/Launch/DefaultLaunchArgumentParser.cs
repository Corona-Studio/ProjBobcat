using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
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
            IVersionLocator versionLocator, AuthResultBase authResult, string rootPath, string rootVersion)
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
                    $"{GamePathHelper.GetLibraryPath(l.Path.Replace('/', '\\'))};"));
            ClassPath += string.IsNullOrEmpty(rootVersion)
                ? GamePathHelper.GetGameExecutablePath(launchSettings.Version)
                : GamePathHelper.GetGameExecutablePath(rootVersion);
            LastAuthResult = LaunchSettings.Authenticator.GetLastAuthResult();
        }

        public string ParseJvmHeadArguments()
        {
            var sb = new StringBuilder();

            if (LaunchSettings == null ||
                LaunchSettings.GameArguments == null && LaunchSettings.FallBackGameArguments == null)
                throw new ArgumentNullException("重要参数为Null!");

            var gameArgs = LaunchSettings.GameArguments ?? LaunchSettings.FallBackGameArguments;
            /*
            var fallBack = LaunchSettings.FallBackGameArguments;
            var major = LaunchSettings.GameArguments;
            var gameSettings = new GameArguments {
                JavaExecutable = string.IsNullOrEmpty(fallBack?.JavaExecutable) ? major.JavaExecutable : fallBack.JavaExecutable,
                MinMemory = fallBack?.MinMemory > major?.MinMemory ? fallBack?.MinMemory : major?.MinMemory,
                MaxMemory = 

            };
            */

            if (!string.IsNullOrEmpty(gameArgs.AgentPath))
            {
                sb.AppendFormat("-javaagent:\"{0}\"", gameArgs.AgentPath);
                if (!string.IsNullOrEmpty(gameArgs.JavaAgentAdditionPara))
                    sb.AppendFormat("={0}", gameArgs.JavaAgentAdditionPara);

                sb.Append(' ');
            }
            else
            {
                if (!string.IsNullOrEmpty(gameArgs.AgentPath))
                {
                    sb.AppendFormat("-javaagent:\"{0}\"", gameArgs.AgentPath);

                    if (!string.IsNullOrEmpty(gameArgs.JavaAgentAdditionPara))
                        sb.AppendFormat("={0}", gameArgs.JavaAgentAdditionPara);

                    sb.Append(' ');
                }
            }


            if (string.IsNullOrEmpty(GameProfile?.JavaArgs))
            {
                if (gameArgs.MaxMemory > 0)
                {
                    if (gameArgs.MinMemory < gameArgs.MaxMemory)
                    {
                        sb.AppendFormat("-Xms{0}m ", gameArgs.MinMemory);
                        sb.AppendFormat("-Xmx{0}m ", gameArgs.MaxMemory);
                    }
                    else
                    {
                        sb.Append("-Xmx2G ");
                    }
                }
                else
                {
                    sb.Append("-Xmx2G ");
                }


                if (gameArgs.GcType == GcType.Disable)
                    return sb.ToString();

                var gcArg = gameArgs.GcType switch
                {
                    GcType.CmsGc => "-XX:+UseConcMarkSweepGC",
                    GcType.G1Gc => "-XX:+UseG1GC",
                    GcType.ParallelGc => "-XX:+UseParallelGC",
                    GcType.SerialGc => "-XX:+UseSerialGC",
                    GcType.ZGc => "-XX:UseZGC",
                    _ => "-XX:+UseG1GC"
                };

                sb.Append(gcArg).Append(' ');
            }
            else
            {
                sb.Append(GameProfile.JavaArgs).Append(' ');
            }

            /*
            sb.Append(
                    "-XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M")
                .Append(' ');
            */

            return sb.ToString();
        }

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

        public string ParseGameArguments(AuthResultBase authResult)
        {
            var mcArgumentsDic = new Dictionary<string, string>
            {
                {"${version_name}", LaunchSettings.Version},
                {"${version_type}", GameProfile?.Type ?? $"\"{LaunchSettings.LauncherName}\""},
                {"${assets_root}", $"\"{AssetRoot}\""},
                {"${assets_index_name}", $"\"{VersionInfo.AssetInfo.Id}\""},
                {
                    "${game_directory}",
                    $"\"{GamePathHelper.GetGamePath(LaunchSettings.Version)}\""
                },
                {"${auth_player_name}", authResult?.SelectedProfile?.Name},
                {"${auth_uuid}", authResult?.SelectedProfile?.UUID.ToString()},
                {"${auth_access_token}", authResult?.AccessToken},
                {"${user_properties}", authResult?.User?.Properties.ResolveUserProperties()},
                {"${user_type}", "Mojang"} // use default value as placeholder
            };

            return StringHelper.ReplaceByDic(VersionInfo.GameArguments, mcArgumentsDic);
        }

        public List<string> GenerateLaunchArguments()
        {
            var arguments = new List<string>
            {
                (string.IsNullOrEmpty(GameProfile?.JavaDir)
                    ? LaunchSettings.FallBackGameArguments?.JavaExecutable ??
                      LaunchSettings.GameArguments.JavaExecutable
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
                sb.AppendFormat("--width {0} ",
                    GameProfile.Resolution?.Width ?? LaunchSettings.GameArguments.Resolution.Width);
                sb.AppendFormat(
                    "--height {0} ", GameProfile.Resolution?.Height ?? LaunchSettings.GameArguments.Resolution.Height);
            }
            else if ((LaunchSettings.FallBackGameArguments?.Resolution?.Width ?? 0) > 0
                     && (LaunchSettings.FallBackGameArguments?.Resolution?.Height ?? 0) > 0)
            {
                sb.AppendFormat(
                    "--width {0} ", LaunchSettings.FallBackGameArguments.Resolution.Width);
                sb.AppendFormat(
                    "--height {0} ", LaunchSettings.FallBackGameArguments.Resolution.Height);
            }

            if (LaunchSettings.GameArguments.ServerSettings == null &&
                LaunchSettings.FallBackGameArguments?.ServerSettings == null) return sb.ToString();

            var serverSettings = LaunchSettings.GameArguments.ServerSettings ??
                                 LaunchSettings.FallBackGameArguments.ServerSettings;

            if (string.IsNullOrEmpty(serverSettings.Address)) return sb.ToString();

            sb.AppendFormat("--server {0} ", serverSettings.Address);
            sb.AppendFormat("--port {0}", serverSettings.Port);

            return sb.ToString();
        }
    }
}