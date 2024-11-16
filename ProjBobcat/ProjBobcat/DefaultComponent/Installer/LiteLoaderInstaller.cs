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
    const string SnapshotRoot = "https://dl.liteloader.com/versions/";
    const string ReleaseRoot = "https://repo.mumfrey.com/content/repositories/liteloader/";

    public required RawVersionModel InheritVersion { get; init; }
    public required LiteLoaderDownloadVersionModel VersionModel { get; init; }
    public override required string RootPath { get; init; }

    public string Install()
    {
        return this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<string> InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(this.RootPath))
            throw new NullReferenceException("RootPath 不能为 null");
        if (this.InheritVersion == null)
            throw new NullReferenceException("InheritVersion 不能为 null");
        if (this.VersionModel == null)
            throw new NullReferenceException("VersionModel 不能为 null");

        this.InvokeStatusChangedEvent("开始安装 LiteLoader", 0);

        var vl = new DefaultVersionLocator(this.RootPath, Guid.Empty);
        var rawVersion = vl.ParseRawVersion(this.VersionModel.McVersion);

        this.InvokeStatusChangedEvent("解析版本", 10);

        if (rawVersion == null)
            throw new UnknownGameNameException(this.VersionModel.McVersion);

        if (rawVersion.Id != this.VersionModel.McVersion)
            throw new NotSupportedException("LiteLoader 并不支持这个 MineCraft 版本");

        var id = string.IsNullOrEmpty(this.CustomId)
            ? $"{this.VersionModel.McVersion}-LiteLoader{this.VersionModel.McVersion}-{this.VersionModel.Version}"
            : this.CustomId;

        var timeStamp = long.TryParse(this.VersionModel.Build.Timestamp, out var timeResult) ? timeResult : 0;
        var time = TimeHelper.Unix11ToDateTime(timeStamp);

        this.InvokeStatusChangedEvent("解析 Libraries", 30);

        var libraries = new List<Library>
        {
            new()
            {
                Name = $"com.mumfrey:liteloader:{this.VersionModel.Version}",
                Url = this.VersionModel.Type.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase)
                    ? SnapshotRoot
                    : ReleaseRoot
            }
        };

        foreach (var lib in this.VersionModel.Build.Libraries
                     .Where(lib => !string.IsNullOrEmpty(lib.Name) && string.IsNullOrEmpty(lib.Url)).Where(lib =>
                         lib.Name.StartsWith("org.ow2.asm", StringComparison.OrdinalIgnoreCase)))
            lib.Url = "https://files.minecraftforge.net/maven/";

        libraries.AddRange(this.VersionModel.Build.Libraries);

        this.InvokeStatusChangedEvent("Libraries 解析完成", 60);

        const string mainClass = "net.minecraft.launchwrapper.Launch";
        var resultModel = new RawVersionModel
        {
            Id = id,
            Time = time,
            ReleaseTime = time,
            Libraries = [.. libraries],
            MainClass = mainClass,
            InheritsFrom = string.IsNullOrEmpty(this.InheritsFrom) ? this.VersionModel.McVersion : this.InheritsFrom,
            BuildType = this.VersionModel.Type,
            JarFile = this.InheritVersion.JarFile ?? this.InheritVersion.Id
        };

        if (this.InheritVersion.Arguments != null)
            resultModel.Arguments = new Arguments
            {
                Game =
                [
                    JsonSerializer.SerializeToElement("--tweakClass", StringContext.Default.String),
                    JsonSerializer.SerializeToElement(this.VersionModel.Build.TweakClass, StringContext.Default.String)
                ]
            };
        else
            resultModel.MinecraftArguments =
                $"{this.InheritVersion.MinecraftArguments} --tweakClass {this.VersionModel.Build.TweakClass}";

        var gamePath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(gamePath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        var jsonPath = GamePathHelper.GetGameJsonPath(this.RootPath, id);
        var jsonContent = JsonSerializer.Serialize(resultModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        this.InvokeStatusChangedEvent("LiteLoader 安装完成", 100);

        return id;
    }
}