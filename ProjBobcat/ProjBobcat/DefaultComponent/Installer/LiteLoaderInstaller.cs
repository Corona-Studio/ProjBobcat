using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LiteLoader;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Event;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class LiteLoaderInstaller : ILiteLoaderInstaller
    {
        private const string SnapshotRoot = "http://dl.liteloader.com/versions/";
        private const string ReleaseRoot = "http://repo.mumfrey.com/content/repositories/liteloader/";

        public string RootPath { get; set; }
        public LiteLoaderDownloadVersionModel VersionModel { get; set; }

        public event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;

        public string Install()
        {
            return InstallTaskAsync().Result;
        }

        public async Task<string> InstallTaskAsync()
        {
            InvokeStageChangedEvent("开始安装 LiteLoader", 0);

            var vl = new DefaultVersionLocator(RootPath, Guid.Empty);
            var rawVersion = vl.ParseRawVersion(VersionModel.McVersion);

            InvokeStageChangedEvent("解析版本", 10);

            if (rawVersion == null)
                throw new UnknownGameNameException(VersionModel.McVersion);

            if (rawVersion.Id != VersionModel.McVersion)
                throw new NotSupportedException("LiteLoader 并不支持这个 MineCraft 版本");

            var id = $"{VersionModel.McVersion}-LiteLoader{VersionModel.McVersion}-{VersionModel.Version}";

            var timeStamp = long.TryParse(VersionModel.Build.Timestamp, out var timeResult) ? timeResult : 0;
            var time = TimeHelper.Unix11ToDateTime(timeStamp);

            InvokeStageChangedEvent("解析 Libraries", 30);

            var libraries = new List<Library>
            {
                new Library
                {
                    Name = $"com.mumfrey:liteloader:{VersionModel.Version}",
                    Url = VersionModel.Type.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase)
                        ? SnapshotRoot
                        : ReleaseRoot
                }
            };

            foreach (var lib in VersionModel.Build.Libraries
                .Where(lib => !string.IsNullOrEmpty(lib.Name) && string.IsNullOrEmpty(lib.Url)).Where(lib =>
                    lib.Name.StartsWith("org.ow2.asm", StringComparison.OrdinalIgnoreCase)))
                lib.Url = "https://files.minecraftforge.net/maven/";

            libraries.AddRange(VersionModel.Build.Libraries);

            InvokeStageChangedEvent("Libraries 解析完成", 60);

            const string mainClass = "net.minecraft.launchwrapper.Launch";
            var resultModel = new RawVersionModel
            {
                Id = id,
                Time = time,
                ReleaseTime = time,
                Libraries = libraries,
                MainClass = mainClass,
                MinecraftArguments = $"--tweakClass {VersionModel.Build.TweakClass}",
                InheritsFrom = VersionModel.McVersion
            };

            var gamePath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
            var di = new DirectoryInfo(gamePath);

            if (!di.Exists)
                di.Create();
            else
                DirectoryHelper.CleanDirectory(di.FullName);

            var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
            var jsonContent = JsonConvert.SerializeObject(resultModel, JsonHelper.CamelCasePropertyNamesSettings);

            await File.WriteAllTextAsync(jsonPath, jsonContent);

            InvokeStageChangedEvent("LiteLoader 安装完成", 100);

            return id;
        }

        private void InvokeStageChangedEvent(string stage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new InstallerStageChangedEventArgs
            {
                CurrentStage = stage,
                Progress = progress
            });
        }
    }
}