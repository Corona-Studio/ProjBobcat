using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer;

public class FabricInstaller : InstallerBase, IFabricInstaller
{
    public IVersionLocator VersionLocator { get; set; }
    public FabricLoaderArtifactModel LoaderArtifact { get; set; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        InvokeStatusChangedEvent("开始安装", 0);

        var mcVersion = LoaderArtifact.Intermediary.Version;
        var id = CustomId ?? $"{mcVersion}-fabric{LoaderArtifact.Loader.Version}";
        var rawVersion = VersionLocator.GetGame(mcVersion);
        if (rawVersion == null)
            throw new UnknownGameNameException(mcVersion);

        var libraries = new List<Library>
        {
            new()
            {
                Name = LoaderArtifact.Loader.Maven,
                Url = "https://maven.fabricmc.net/"
            },
            new()
            {
                Name = LoaderArtifact.Intermediary.Maven,
                Url = "https://maven.fabricmc.net/"
            }
        };

        libraries.AddRange(LoaderArtifact.LauncherMeta.Libraries.Common);
        libraries.AddRange(LoaderArtifact.LauncherMeta.Libraries.Client);

        var mainClassJObject = (JObject) LoaderArtifact.LauncherMeta.MainClass;
        var mainClass = mainClassJObject.Type switch
        {
            JTokenType.String => mainClassJObject.ToObject<string>(),
            JTokenType.Object => mainClassJObject.ToObject<Dictionary<string, string>>()
                ?.TryGetValue("client", out var outMainClass) ?? false
                ? outMainClass
                : string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(mainClass))
            throw new NullReferenceException("MainClass 字段为空");

        var inheritsFrom = string.IsNullOrEmpty(InheritsFrom) ? mcVersion : InheritsFrom;

        var installPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(installPath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        InvokeStatusChangedEvent("生成版本总成", 70);

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

        InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        InvokeStatusChangedEvent("安装完成", 100);

        return id;
    }
}