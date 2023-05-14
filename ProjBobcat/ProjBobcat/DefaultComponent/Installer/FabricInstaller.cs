using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer;

public class FabricInstaller : InstallerBase, IFabricInstaller
{
    public IVersionLocator VersionLocator { get; init; }
    public FabricLoaderArtifactModel LoaderArtifact { get; set; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        InvokeStatusChangedEvent("开始安装", 0);

        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("RootPath 字段为空");

        var mcVersion = LoaderArtifact.Intermediary.Version;
        var id = CustomId ?? $"{mcVersion}-fabric{LoaderArtifact.Loader.Version}";

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

        var mainClassJObject = LoaderArtifact.LauncherMeta.MainClass;
        var mainClass = mainClassJObject.ValueKind switch
        {
            JsonValueKind.String => mainClassJObject.Deserialize(StringContext.Default.String),
            JsonValueKind.Object => mainClassJObject.Deserialize(DictionaryContext.Default.DictionaryStringString)
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
            Libraries = libraries.ToArray(),
            Arguments = new Arguments(),
            ReleaseTime = DateTime.Now,
            Time = DateTime.Now
        };

        var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonContent = JsonSerializer.Serialize(resultModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        InvokeStatusChangedEvent("安装完成", 100);

        return id;
    }
}