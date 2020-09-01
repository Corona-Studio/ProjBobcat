using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LiteLoader;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Exceptions;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class LiteLoaderInstaller : ILiteLoaderInstaller
    {
        private const string SnapshotRoot = "http://dl.liteloader.com/versions/";
        private const string ReleaseRoot = "http://repo.mumfrey.com/content/repositories/liteloader/";

        public string RootPath { get; set; }

        public event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;

        private void InvokeStageChangedEvent(string stage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new InstallerStageChangedEventArgs
            {
                CurrentStage = stage,
                Progress = progress
            });
        }

        public string Install(LiteLoaderDownloadVersionModel versionModel)
        {
            var vl = new DefaultVersionLocator(RootPath, Guid.Empty);
            var rawVersion = vl.ParseRawVersion(versionModel.McVersion);

            if (rawVersion == null)
                throw new UnknownGameNameException(versionModel.McVersion);

            if (rawVersion.Id != versionModel.McVersion)
                throw new NotSupportedException("LiteLoader 并不支持这个 MineCraft 版本");

            var id = $"{versionModel.McVersion}-LiteLoader{versionModel.McVersion}-{versionModel.Version}";

            var timeStamp = long.TryParse(versionModel.Build.Timestamp, out var timeResult) ? timeResult : 0;
            var time = TimeHelper.Unix11ToDateTime(timeStamp);

            var libraries = new List<Library>
            {
                new Library
                {
                    Name = $"com.mumfrey:liteloader:{versionModel.Version}",
                    Url = versionModel.Type.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase)
                        ? SnapshotRoot
                        : ReleaseRoot
                }
            };

            foreach (var lib in versionModel.Build.Libraries
                .Where(lib => !string.IsNullOrEmpty(lib.Name) && string.IsNullOrEmpty(lib.Url)).Where(lib =>
                    lib.Name.StartsWith("org.ow2.asm", StringComparison.OrdinalIgnoreCase)))
            {
                lib.Url = "https://files.minecraftforge.net/maven/";
            }

            libraries.AddRange(versionModel.Build.Libraries);

            const string mainClass = "net.minecraft.launchwrapper.Launch";
            var resultModel = new RawVersionModel
            {
                Id = id,
                Time = time,
                ReleaseTime = time,
                Libraries = libraries,
                MainClass = mainClass,
                MinecraftArguments = $"--tweakClass {versionModel.Build.TweakClass}",
                InheritsFrom = versionModel.McVersion
            };

            var gamePath = GamePathHelper.GetGamePath(RootPath, id);
            var di = new DirectoryInfo(gamePath);

            if (!di.Exists)
                di.Create();
            else
                DirectoryHelper.CleanDirectory(di.FullName);

            var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
            var jsonContent = JsonConvert.SerializeObject(resultModel, JsonHelper.CamelCasePropertyNamesSettings);

            File.WriteAllText(jsonPath, jsonContent);

            return id;
        }

        public Task<string> InstallTaskAsync(LiteLoaderDownloadVersionModel versionModel)
        {
            return Task.Run(() => Install(versionModel));
        }
    }
}