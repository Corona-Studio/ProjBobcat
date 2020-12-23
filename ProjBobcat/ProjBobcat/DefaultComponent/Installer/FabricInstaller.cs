using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class FabricInstaller : InstallerBase, IFabricInstaller
    {
        public string CustomId { get; set; }
        public FabricLoaderArtifactModel LoaderArtifact { get; set; }
        public FabricArtifactModel YarnArtifact { get; set; }

        public string Install()
        {
            /*
            InvokeStageChangedEvent("开始安装", 0);

            var jsonUrl = "https://fabricmc.net/download/technic/?yarn="
                + Uri.EscapeDataString(YarnArtifact.Version)
                + "&loader="
                + Uri.EscapeDataString(LoaderArtifact.Loader.Version);

            var mcVersion = LoaderArtifact.Intermediary.Version;
            var vl = new DefaultVersionLocator(RootPath, Guid.Empty);

            var rawVersion = vl.ParseRawVersion(mcVersion);
            if (rawVersion == null)
                throw new UnknownGameNameException(mcVersion);

            var id = $"{mcVersion}-fabric{LoaderArtifact.Loader.Version}";

            var libraries = new List<Library>
            {
                new Library
                {
                    Name = LoaderArtifact.Loader.Maven,
                    Url = "https://maven.fabricmc.net/"
                },
                new Library
                {
                    Name = LoaderArtifact.Intermediary.Maven,
                    Url = "https://maven.fabricmc.net/"
                }
            };

            libraries.AddRange(LoaderArtifact.LauncherMeta.Libraries.Common);
            libraries.AddRange(LoaderArtifact.LauncherMeta.Libraries.Client);

            InvokeStageChangedEvent("解析 Libraries 完成", 23.3333);

            var mainClass = LoaderArtifact.LauncherMeta.MainClass switch
            {
                string mainClassString => mainClassString,
                Dictionary<string, string> dic => dic["client"],
                _ => string.Empty
            };

            if(string.IsNullOrEmpty(mainClass))
                throw new NullReferenceException("MainClass 字段为空");

            var inheritsFrom = mcVersion;

            var installPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
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
            */

            return InstallTaskAsync().Result;
        }

        public async Task<string> InstallTaskAsync()
        {
            InvokeStatusChangedEvent("开始安装", 0);

            var jsonUrl = "https://fabricmc.net/download/technic/?yarn="
                          + Uri.EscapeDataString(YarnArtifact.Version)
                          + "&loader="
                          + Uri.EscapeDataString(LoaderArtifact.Loader.Version);
            var jsonContent = await HttpHelper.Get(jsonUrl);
            var versionModel = JsonConvert.DeserializeObject<RawVersionModel>(jsonContent);
            var id = string.IsNullOrEmpty(CustomId) ?
                    $"{YarnArtifact.GameVersion}-fabric{YarnArtifact.Version}-{LoaderArtifact.Loader.Version}"
                    : CustomId;
            
            versionModel.Id = id;
            versionModel.InheritsFrom = YarnArtifact.GameVersion;

            InvokeStatusChangedEvent("解析 Libraries 完成", 23.3333);

            var dir = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
            var di = new DirectoryInfo(dir);

            if (!di.Exists)
                di.Create();
            else
                DirectoryHelper.CleanDirectory(di.FullName);

            var resultJson = JsonConvert.SerializeObject(versionModel, JsonHelper.CamelCasePropertyNamesSettings);
            InvokeStatusChangedEvent("生成版本总成", 70);
            var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);

            InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

            await File.WriteAllTextAsync(jsonPath, resultJson);

            InvokeStatusChangedEvent("安装完成", 100);

            return id;
        }
    }
}