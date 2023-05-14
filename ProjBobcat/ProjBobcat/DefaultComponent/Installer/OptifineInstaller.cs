using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer;

public class OptifineInstaller : InstallerBase, IOptifineInstaller
{
    public string? JavaExecutablePath { get; set; }
    public string? OptifineJarPath { get; set; }
    public OptifineDownloadVersionModel? OptifineDownloadVersion { get; set; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(JavaExecutablePath))
            throw new NullReferenceException("未指定 Java 运行时");
        if (string.IsNullOrEmpty(OptifineJarPath))
            throw new NullReferenceException("未指定 Optifine 安装包路径");
        if (OptifineDownloadVersion == null)
            throw new NullReferenceException("未指定 Optifine 下载信息");

        InvokeStatusChangedEvent("开始安装 Optifine", 0);
        var mcVersion = OptifineDownloadVersion.McVersion;
        var edition = OptifineDownloadVersion.Type;
        var release = OptifineDownloadVersion.Patch;
        var editionRelease = $"{edition}_{release}";
        var id = string.IsNullOrEmpty(CustomId)
            ? $"{mcVersion}-Optifine_{editionRelease}"
            : CustomId;

        var versionPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(versionPath);

        if (!di.Exists)
            di.Create();

        InvokeStatusChangedEvent("读取 Optifine 数据", 20);
        using var archive = ArchiveFactory.Open(OptifineJarPath);
        var entries = archive.Entries;

        var launchWrapperVersion = "1.12";
        var launchWrapperOfEntry =
            entries.FirstOrDefault(e => e.Key.Equals("launchwrapper-of.txt", StringComparison.OrdinalIgnoreCase));

        if (launchWrapperOfEntry != null)
        {
            await using var stream = launchWrapperOfEntry.OpenEntryStream();
            using var sr = new StreamReader(stream, Encoding.UTF8);
            launchWrapperVersion = await sr.ReadToEndAsync();
        }

        var launchWrapperEntry =
            entries.FirstOrDefault(x => x.Key.Equals($"launchwrapper-of-{launchWrapperVersion}.jar"));

        InvokeStatusChangedEvent("生成版本总成", 40);

        var versionModel = new RawVersionModel
        {
            Id = id,
            InheritsFrom = InheritsFrom ?? mcVersion,
            Arguments = new Arguments
            {
                Game = new[]
                {
                    JsonSerializer.SerializeToElement("--tweakClass", StringContext.Default.String),
                    JsonSerializer.SerializeToElement("optifine.OptiFineTweaker", StringContext.Default.String)
                },
                Jvm = Array.Empty<JsonElement>()
            },
            ReleaseTime = DateTime.Now,
            Time = DateTime.Now,
            BuildType = "release",
            Libraries = new[]
            {
                new Library
                {
                    Name = launchWrapperVersion == "1.12"
                        ? "net.minecraft:launchwrapper:1.12"
                        : $"optifine:launchwrapper-of:{launchWrapperVersion}"
                },
                new Library
                {
                    Name = $"optifine:Optifine:{OptifineDownloadVersion.McVersion}_{editionRelease}"
                }
            },
            MainClass = "net.minecraft.launchwrapper.Launch",
            MinimumLauncherVersion = 21
        };

        var versionJsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonStr = JsonSerializer.Serialize(versionModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));
        await File.WriteAllTextAsync(versionJsonPath, jsonStr);

        var librariesPath = Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath(), "optifine",
            "launchwrapper-of",
            launchWrapperVersion);
        var libDi = new DirectoryInfo(librariesPath);

        InvokeStatusChangedEvent("写入 Optifine 数据", 60);

        if (!libDi.Exists)
            libDi.Create();

        var launchWrapperPath = Path.Combine(librariesPath,
            $"launchwrapper-of-{launchWrapperVersion}.jar");
        if (!File.Exists(launchWrapperPath) && launchWrapperEntry != null)
        {
            InvokeStatusChangedEvent($"解压 launcherwrapper-{launchWrapperVersion} 数据", 65);

            await using var launchWrapperFs = File.OpenWrite(launchWrapperPath);
            launchWrapperEntry.WriteTo(launchWrapperFs);
        }

        var gameJarPath = Path.Combine(RootPath,
            GamePathHelper.GetGameExecutablePath(InheritsFrom ?? OptifineDownloadVersion.McVersion));
        var optifineLibPath = Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath(), "optifine", "Optifine",
            $"{OptifineDownloadVersion.McVersion}_{editionRelease}",
            $"Optifine-{OptifineDownloadVersion.McVersion}_{editionRelease}.jar");

        var optifineLibPathDi = new DirectoryInfo(Path.GetDirectoryName(optifineLibPath)!);
        if (!optifineLibPathDi.Exists)
            optifineLibPathDi.Create();

        InvokeStatusChangedEvent("执行安装脚本", 80);

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
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var p = Process.Start(ps);

        p!.BeginOutputReadLine();
        p.BeginErrorReadLine();

        void LogReceivedEvent(object sender, DataReceivedEventArgs args)
        {
            InvokeStatusChangedEvent(args.Data ?? "loading...", 85);
        }

        p.OutputDataReceived += LogReceivedEvent;

        var errList = new List<string>();
        p.ErrorDataReceived += (sender, args) =>
        {
            LogReceivedEvent(sender, args);

            if (!string.IsNullOrEmpty(args.Data))
                errList.Add(args.Data);
        };

        await p.WaitForExitAsync();
        InvokeStatusChangedEvent("安装即将完成", 90);

        if (errList.Any())
            throw new NullReferenceException(string.Join(Environment.NewLine, errList));

        InvokeStatusChangedEvent("Optifine 安装完成", 100);

        return id;
    }
}