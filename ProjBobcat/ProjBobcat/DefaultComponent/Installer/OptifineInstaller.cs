using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class OptifineInstaller : InstallerBase, IOptifineInstaller
    {
        public string JavaExecutablePath { get; set; }
        public string OptifineJarPath { get; set; }
        public OptifineDownloadVersionModel OptifineDownloadVersion { get; set; }

        public string Install()
        {
            return InstallTaskAsync().Result;
        }

        public async Task<string> InstallTaskAsync()
        {
            InvokeStatusChangedEvent("开始安装 Optifine", 0);
            var mcVersion = OptifineDownloadVersion.McVersion;
            var edition = OptifineDownloadVersion.Type;
            var release = OptifineDownloadVersion.Patch;
            var editionRelease = $"{edition}_{release}";
            var id = $"{mcVersion}-Optifine_{editionRelease}";

            var versionPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
            var di = new DirectoryInfo(versionPath);

            if (!di.Exists)
                di.Create();

            InvokeStatusChangedEvent("读取 Optifine 数据", 20);
            using var archive = ArchiveFactory.Open(OptifineJarPath);
            var entries = archive.Entries;

            var launchWrapperVersion = string.Empty;

            foreach (var entry in entries)
            {
                if (!entry.Key.Equals("launchwrapper-of.txt", StringComparison.OrdinalIgnoreCase)) continue;
                await using var stream = entry.OpenEntryStream();
                using var sr = new StreamReader(stream, Encoding.UTF8);
                launchWrapperVersion = await sr.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(launchWrapperVersion))
                throw new NullReferenceException("launchwrapper-of.txt 未找到");

            var launchWrapperEntry =
                entries.First(x => x.Key.Equals($"launchwrapper-of-{launchWrapperVersion}.jar"));

            InvokeStatusChangedEvent("生成版本总成", 40);

            var versionModel = new RawVersionModel
            {
                Id = id,
                InheritsFrom = mcVersion,
                Arguments = new Arguments
                {
                    Game = new List<object>
                    {
                        "--tweakClass",
                        "optifine.OptiFineTweaker"
                    },
                    Jvm = new List<object>()
                },
                ReleaseTime = DateTime.Now,
                Time = DateTime.Now,
                BuildType = "release",
                Libraries = new List<Library>
                {
                    new()
                    {
                        Name = $"optifine:launchwrapper-of:{launchWrapperVersion}"
                    },
                    new()
                    {
                        Name = $"optifine:Optifine:{OptifineDownloadVersion.McVersion}_{editionRelease}"
                    }
                },
                MainClass = "net.minecraft.launchwrapper.Launch",
                MinimumLauncherVersion = 21
            };

            var versionJsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
            var jsonStr = JsonConvert.SerializeObject(versionModel, JsonHelper.CamelCasePropertyNamesSettings);
            await File.WriteAllTextAsync(versionJsonPath, jsonStr);

            var librariesPath = Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath(), "launchwrapper-of",
                launchWrapperVersion);
            var libDi = new DirectoryInfo(librariesPath);

            InvokeStatusChangedEvent("写入 Optifine 数据", 60);

            if (!libDi.Exists)
                libDi.Create();

            launchWrapperEntry.WriteToDirectory(Path.Combine(librariesPath,
                $"launchwrapper-of-{launchWrapperVersion}.jar"));

            var gameJarPath = Path.Combine(RootPath,
                GamePathHelper.GetGameExecutablePath(OptifineDownloadVersion.McVersion));
            var optifineLibPath = Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath(), "optifine", "Optifine",
                $"{OptifineDownloadVersion.McVersion}_{editionRelease}",
                $"Optifine-{OptifineDownloadVersion.McVersion}_{editionRelease}.jar");

            var ps = new ProcessStartInfo(JavaExecutablePath)
            {
                ArgumentList =
                {
                    "-cp",
                    OptifineJarPath,
                    "optifine.Patcher",
                    Path.GetFullPath(gameJarPath),
                    OptifineJarPath,
                    Path.GetFullPath(optifineLibPath)
                }
            };
            var p = Process.Start(ps);

            if (p == null)
                throw new NullReferenceException();

            await p.WaitForExitAsync();
            var err = await p.StandardError.ReadToEndAsync();

            InvokeStatusChangedEvent("安装即将完成", 90);

            if (!string.IsNullOrEmpty(err))
                throw new NullReferenceException();

            InvokeStatusChangedEvent("Optifine 安装完成", 0);

            return id;
        }
    }
}