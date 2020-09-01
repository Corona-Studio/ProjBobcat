using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Exceptions;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class FabricInstaller : IFabricInstaller
    {
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

        public string Install(FabricLoaderArtifactModel loaderArtifact)
        {
            InvokeStageChangedEvent("开始安装", 0);
            var mcVersion = loaderArtifact.Intermediary.Version;
            var vl = new DefaultVersionLocator(RootPath, Guid.Empty);

            var rawVersion = vl.ParseRawVersion(mcVersion);
            if (rawVersion == null)
                throw new UnknownGameNameException(mcVersion);

            var id = $"{mcVersion}-fabric{loaderArtifact.Loader.Version}";

            var libraries = new List<Library>
            {
                new Library
                {
                    Name = loaderArtifact.Loader.Maven,
                    Url = "https://maven.fabricmc.net/"
                },
                new Library
                {
                    Name = loaderArtifact.Intermediary.Maven,
                    Url = "https://maven.fabricmc.net/"
                }
            };

            libraries.AddRange(loaderArtifact.LauncherMeta.Libraries.Common);
            libraries.AddRange(loaderArtifact.LauncherMeta.Libraries.Client);

            InvokeStageChangedEvent("解析 Libraries 完成", 23.3333);

            var mainClass = loaderArtifact.LauncherMeta.MainClass.Client;
            var inheritsFrom = mcVersion;

            var installPath = GamePathHelper.GetGamePath(RootPath, id);
            var di = new DirectoryInfo(installPath);

            if (!di.Exists)
                di.Create();
            else
                DirectoryHelper.CleanDirectory(di.FullName);

            InvokeStageChangedEvent("生成版本总成", 70);

            var resultModel = new RawVersionModel
            {
                Id = id,
                InheritsFrom = inheritsFrom,
                MainClass = mainClass,
                Libraries = libraries,
                Arguments = new Arguments(),
                ReleaseTime = DateTime.Now,
                Time = DateTime.Now
            };

            var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
            var jsonContent = JsonConvert.SerializeObject(resultModel, JsonHelper.CamelCasePropertyNamesSettings);

            InvokeStageChangedEvent("将版本 Json 写入文件", 90);

            File.WriteAllText(jsonPath, jsonContent);

            InvokeStageChangedEvent("安装完成", 100);

            return id;
        }

        public Task<string> InstallTaskAsync(FabricLoaderArtifactModel loaderArtifact)
        {
            return Task.Run(() => Install(loaderArtifact));
        }
    }
}