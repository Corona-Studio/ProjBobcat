using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly LaunchSettings _launchSettings;

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

            _launchSettings = launchSettings;

            AuthResult = authResult;
            VersionLocator = versionLocator;
            RootPath = rootPath;
            LaunchSettings = launchSettings;
            LauncherProfileParser = launcherProfileParser;
            VersionInfo = LaunchSettings.VersionLocator.GetGame(LaunchSettings.Version);
            GameProfile = LauncherProfileParser.GetGameProfile(LaunchSettings.GameName);

            var sb = new StringBuilder();
            foreach (var lib in VersionInfo.Libraries)
            {
                sb.AppendFormat("{0};", Path.Combine(RootPath, GamePathHelper.GetLibraryPath(lib.Path).Replace('/', '\\')));
            }
            
            var rootJarPath = string.IsNullOrEmpty(rootVersion)
                ? GamePathHelper.GetGameExecutablePath(launchSettings.Version)
                : GamePathHelper.GetGameExecutablePath(rootVersion);
            sb.Append(Path.Combine(RootPath, rootJarPath));

            ClassPath = sb.ToString();
            LastAuthResult = LaunchSettings.Authenticator.GetLastAuthResult();
        }

        public IEnumerable<string> ParseJvmHeadArguments()
        {
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
                yield return $"-javaagent:\"{gameArgs.AgentPath}\"";
                if (!string.IsNullOrEmpty(gameArgs.JavaAgentAdditionPara))
                    yield return $"={gameArgs.JavaAgentAdditionPara}";
            }

            if (string.IsNullOrEmpty(GameProfile?.JavaArgs))
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
                    GcType.ZGc => "-XX:UseZGC",
                    _ => "-XX:+UseG1GC"
                };

                yield return gcArg;
            }
            else
            {
                yield return GameProfile.JavaArgs;
            }

            /*
            sb.Append(
                    "-XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M")
                .Append(' ');
            */
        }

        public IEnumerable<string> ParseJvmArguments()
        {
            var jvmArgumentsDic = new Dictionary<string, string>
            {
                {"${natives_directory}", $"\"{NativeRoot}\""},
                {"${launcher_name}", $"\"{LaunchSettings.LauncherName}\""},
                {"${launcher_version}", "21"},
                {"${classpath}", $"\"{ClassPath}\""},
                {"${classpath_separator}", ";"},
                {"${library_directory}", Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath())}
            };

            if (VersionInfo.JvmArguments?.Any() ?? false)
            {
                foreach (var jvmArg in VersionInfo.JvmArguments)
                {
                    yield return StringHelper.ReplaceByDic(jvmArg, jvmArgumentsDic);
                }
            }

            const string preset =
                "[{rules: [{action: \"allow\",os:{name: \"osx\"}}],value: [\"-XstartOnFirstThread\"]},{rules: [{action: \"allow\",os:{name: \"windows\"}}],value: \"-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump\"},{rules: [{action: \"allow\",os:{name: \"windows\",version: \"^10\\\\.\"}}],value: [\"-Dos.name=Windows 10\",\"-Dos.version=10.0\"]},\"-Djava.library.path=${natives_directory}\",\"-Dminecraft.launcher.brand=${launcher_name}\",\"-Dminecraft.launcher.version=${launcher_version}\",\"-cp\",\"${classpath}\"]";
            var preJvmArguments = VersionLocator.ParseJvmArguments(JsonConvert.DeserializeObject<List<object>>(preset));


            foreach (var preJvmArg in preJvmArguments)
            {
                yield return StringHelper.ReplaceByDic(preJvmArg, jvmArgumentsDic);
            }
        }

        public IEnumerable<string> ParseGameArguments(AuthResultBase authResult)
        {
            var gameDir = _launchSettings.VersionInsulation
                ? Path.Combine(RootPath, GamePathHelper.GetGamePath(LaunchSettings.Version))
                : RootPath;
            var mcArgumentsDic = new Dictionary<string, string>
            {
                {"${version_name}", $"\"{LaunchSettings.Version}\""},
                {"${version_type}", GameProfile?.Type ?? $"\"{LaunchSettings.LauncherName}\""},
                {"${assets_root}", $"\"{AssetRoot}\""},
                {"${assets_index_name}", $"\"{VersionInfo.AssetInfo.Id}\""},
                {"${game_directory}", $"\"{gameDir}\""},
                {"${auth_player_name}", authResult?.SelectedProfile?.Name},
                {"${auth_uuid}", authResult?.SelectedProfile?.UUID.ToString()},
                {"${auth_access_token}", authResult?.AccessToken},
                {"${user_properties}", authResult?.User?.Properties.ResolveUserProperties()},
                {"${user_type}", "Mojang"} // use default value as placeholder
            };

            foreach (var gameArg in VersionInfo.GameArguments)
            {
                yield return StringHelper.ReplaceByDic(gameArg, mcArgumentsDic);
            }
        }

        public List<string> GenerateLaunchArguments()
        {
            var javaPath = GameProfile?.JavaDir;
            if (string.IsNullOrEmpty(javaPath))
            {
                javaPath = LaunchSettings.FallBackGameArguments?.JavaExecutable ??
                           LaunchSettings.GameArguments?.JavaExecutable;
            }

            var arguments = new List<string>
            {
                javaPath
            };

            arguments.AddRange(ParseJvmHeadArguments().Select(arg => arg.Trim()));
            arguments.AddRange(ParseJvmArguments().Select(arg => arg.Trim()));

            arguments.Add(VersionInfo.MainClass);

            arguments.AddRange(ParseGameArguments(AuthResult).Select(arg => arg.Trim()));
            arguments.AddRange(ParseAdditionalArguments().Select(arg => arg.Trim()));
            
            return arguments;
        }

        /// <summary>
        ///     解析额外参数（分辨率，服务器地址）
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ParseAdditionalArguments()
        {
            if (!VersionInfo.AvailableGameArguments.Any()) yield break;
            if (!VersionInfo.AvailableGameArguments.ContainsKey("has_custom_resolution")) yield break;

            if ((LaunchSettings.GameArguments?.Resolution?.Height ?? 0) > 0 &&
                (LaunchSettings.GameArguments?.Resolution?.Width ?? 0) > 0)
            {
                yield return string.Format("--width {0} ",
                    GameProfile.Resolution?.Width ?? LaunchSettings.GameArguments.Resolution.Width);
                
                yield return string.Format(
                    "--height {0} ", GameProfile.Resolution?.Height ?? LaunchSettings.GameArguments.Resolution.Height);
            }
            else if ((LaunchSettings.FallBackGameArguments?.Resolution?.Width ?? 0) > 0
                     && (LaunchSettings.FallBackGameArguments?.Resolution?.Height ?? 0) > 0)
            {
                yield return string.Format(
                    "--width {0} ", LaunchSettings.FallBackGameArguments.Resolution.Width);
                
                yield return string.Format(
                    "--height {0} ", LaunchSettings.FallBackGameArguments.Resolution.Height);
            }

            if (LaunchSettings.GameArguments.ServerSettings == null &&
                LaunchSettings.FallBackGameArguments?.ServerSettings == null) yield break;

            var serverSettings = LaunchSettings.GameArguments.ServerSettings ??
                                 LaunchSettings.FallBackGameArguments.ServerSettings;

            if (string.IsNullOrEmpty(serverSettings.Address)) yield break;

            yield return string.Format("--server {0} ", serverSettings.Address);
            yield return string.Format("--port {0}", serverSettings.Port);
        }
    }
}