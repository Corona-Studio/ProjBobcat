using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Class.Model.LiteLoader;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer;

public class LiteLoaderInstaller : InstallerBase, ILiteLoaderInstaller
{
    const string SnapshotRoot = "http://dl.liteloader.com/versions/";
    const string ReleaseRoot = "http://repo.mumfrey.com/content/repositories/liteloader/";

    public RawVersionModel InheritVersion { get; init; }
    public LiteLoaderDownloadVersionModel VersionModel { get; init; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("RootPath 不能为 null");
        if (InheritVersion == null)
            throw new NullReferenceException("InheritVersion 不能为 null");
        if (VersionModel == null)
            throw new NullReferenceException("VersionModel 不能为 null");

        InvokeStatusChangedEvent("开始安装 LiteLoader", 0);

        var vl = new DefaultVersionLocator(RootPath, Guid.Empty);
        var rawVersion = vl.ParseRawVersion(VersionModel.McVersion);

        InvokeStatusChangedEvent("解析版本", 10);

        if (rawVersion == null)
            throw new UnknownGameNameException(VersionModel.McVersion);

        if (rawVersion.Id != VersionModel.McVersion)
            throw new NotSupportedException("LiteLoader 并不支持这个 MineCraft 版本");

        var id = string.IsNullOrEmpty(CustomId)
            ? $"{VersionModel.McVersion}-LiteLoader{VersionModel.McVersion}-{VersionModel.Version}"
            : CustomId;

        var timeStamp = long.TryParse(VersionModel.Build.Timestamp, out var timeResult) ? timeResult : 0;
        var time = TimeHelper.Unix11ToDateTime(timeStamp);

        InvokeStatusChangedEvent("解析 Libraries", 30);

        var libraries = new List<Library>
        {
            new()
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

        InvokeStatusChangedEvent("Libraries 解析完成", 60);

        const string mainClass = "net.minecraft.launchwrapper.Launch";
        var resultModel = new RawVersionModel
        {
            Id = id,
            Time = time,
            ReleaseTime = time,
            Libraries = libraries.ToArray(),
            MainClass = mainClass,
            InheritsFrom = string.IsNullOrEmpty(InheritsFrom) ? VersionModel.McVersion : InheritsFrom,
            BuildType = VersionModel.Type,
            JarFile = InheritVersion.JarFile ?? InheritVersion.Id
        };

        if (InheritVersion.Arguments != null)
            resultModel.Arguments = new Arguments
            {
                Game = new[]
                {
                    JsonSerializer.SerializeToElement("--tweakClass", StringContext.Default.String),
                    JsonSerializer.SerializeToElement(VersionModel.Build.TweakClass, StringContext.Default.String)
                }
            };
        else
            resultModel.MinecraftArguments =
                $"{InheritVersion.MinecraftArguments} --tweakClass {VersionModel.Build.TweakClass}";

        var gamePath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(gamePath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonContent = JsonSerializer.Serialize(resultModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        InvokeStatusChangedEvent("LiteLoader 安装完成", 100);

        return id;
    }
}