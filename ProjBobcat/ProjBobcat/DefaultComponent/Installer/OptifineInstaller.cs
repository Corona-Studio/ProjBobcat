using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer;

public class OptifineInstaller : InstallerBase, IOptifineInstaller
{
    public required string JavaExecutablePath { get; init; }
    public required string OptifineJarPath { get; init; }
    public required OptifineDownloadVersionModel OptifineDownloadVersion { get; init; }
    public override required string RootPath { get; init; }

    public string Install()
    {
        return this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<string> InstallTaskAsync()
    {
        ArgumentException.ThrowIfNullOrEmpty(this.JavaExecutablePath);
        ArgumentException.ThrowIfNullOrEmpty(this.OptifineJarPath);
        ArgumentNullException.ThrowIfNull(this.OptifineDownloadVersion);

        this.InvokeStatusChangedEvent("开始安装 Optifine", ProgressValue.Start);
        var mcVersion = this.OptifineDownloadVersion.McVersion;
        var edition = this.OptifineDownloadVersion.Type;
        var release = this.OptifineDownloadVersion.Patch;
        var editionRelease = $"{edition}_{release}";
        var id = string.IsNullOrEmpty(this.CustomId)
            ? $"{mcVersion}-Optifine_{editionRelease}"
            : this.CustomId;

        var versionPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(versionPath);

        if (!di.Exists)
            di.Create();

        this.InvokeStatusChangedEvent("读取 Optifine 数据", ProgressValue.FromDisplay(20));

        await using var fs = File.OpenRead(this.OptifineJarPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        var entries = archive.Entries;

        var launchWrapperVersion = "1.12";
        var launchWrapperOfEntry =
            entries.FirstOrDefault(e => e.FullName.Equals("launchwrapper-of.txt", StringComparison.OrdinalIgnoreCase));

        if (launchWrapperOfEntry != null)
        {
            await using var stream = launchWrapperOfEntry.Open();
            using var sr = new StreamReader(stream, Encoding.UTF8);
            launchWrapperVersion = await sr.ReadToEndAsync();
        }

        var launchWrapperEntry =
            entries.FirstOrDefault(x => x.FullName.Equals($"launchwrapper-of-{launchWrapperVersion}.jar"));

        this.InvokeStatusChangedEvent("生成版本总成", ProgressValue.FromDisplay(40));

        var versionModel = new RawVersionModel
        {
            Id = id,
            InheritsFrom = this.InheritsFrom ?? mcVersion,
            Arguments = new Arguments
            {
                Game =
                [
                    JsonSerializer.SerializeToElement("--tweakClass", StringContext.Default.String),
                    JsonSerializer.SerializeToElement("optifine.OptiFineTweaker", StringContext.Default.String)
                ],
                Jvm = []
            },
            ReleaseTime = DateTime.Now,
            Time = DateTime.Now,
            BuildType = "release",
            Libraries =
            [
                new Library
                {
                    Name = launchWrapperVersion == "1.12"
                        ? "net.minecraft:launchwrapper:1.12"
                        : $"optifine:launchwrapper-of:{launchWrapperVersion}"
                },
                new Library
                {
                    Name = $"optifine:Optifine:{this.OptifineDownloadVersion.McVersion}_{editionRelease}"
                }
            ],
            MainClass = "net.minecraft.launchwrapper.Launch",
            MinimumLauncherVersion = 21
        };

        var versionJsonPath = GamePathHelper.GetGameJsonPath(this.RootPath, id);
        var jsonStr = JsonSerializer.Serialize(versionModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));
        await File.WriteAllTextAsync(versionJsonPath, jsonStr);

        var librariesPath = Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath(), "optifine",
            "launchwrapper-of",
            launchWrapperVersion);
        var libDi = new DirectoryInfo(librariesPath);

        this.InvokeStatusChangedEvent("写入 Optifine 数据", ProgressValue.FromDisplay(60));

        if (!libDi.Exists)
            libDi.Create();

        var launchWrapperPath = Path.Combine(librariesPath,
            $"launchwrapper-of-{launchWrapperVersion}.jar");
        if (!File.Exists(launchWrapperPath) && launchWrapperEntry != null)
        {
            this.InvokeStatusChangedEvent($"解压 launcherwrapper-{launchWrapperVersion} 数据",
                ProgressValue.FromDisplay(65));

            await using var launchWrapperFs = File.OpenWrite(launchWrapperPath);
            await using var launchWrapperStream = launchWrapperEntry.Open();

            await launchWrapperStream.CopyToAsync(launchWrapperFs);
        }

        var gameJarPath = Path.Combine(this.RootPath,
            GamePathHelper.GetGameExecutablePath(this.InheritsFrom ?? this.OptifineDownloadVersion.McVersion));
        var optifineLibPath = Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath(), "optifine", "Optifine",
            $"{this.OptifineDownloadVersion.McVersion}_{editionRelease}",
            $"Optifine-{this.OptifineDownloadVersion.McVersion}_{editionRelease}.jar");

        var optifineLibPathDi = new DirectoryInfo(Path.GetDirectoryName(optifineLibPath)!);
        if (!optifineLibPathDi.Exists)
            optifineLibPathDi.Create();

        this.InvokeStatusChangedEvent("执行安装脚本", ProgressValue.FromDisplay(80));

        var ps = new ProcessStartInfo(this.JavaExecutablePath)
        {
            ArgumentList =
            {
                "-cp",
                this.OptifineJarPath,
                "optifine.Patcher",
                Path.GetFullPath(gameJarPath),
                this.OptifineJarPath,
                Path.GetFullPath(optifineLibPath)
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var p = Process.Start(ps);

        p!.BeginOutputReadLine();
        p.BeginErrorReadLine();

        p.OutputDataReceived += LogReceivedEvent;

        var errList = new List<string>();
        p.ErrorDataReceived += (sender, args) =>
        {
            LogReceivedEvent(sender, args);

            if (!string.IsNullOrEmpty(args.Data))
                errList.Add(args.Data);
        };

        await p.WaitForExitAsync();
        this.InvokeStatusChangedEvent("安装即将完成", ProgressValue.FromDisplay(95));

        ArgumentOutOfRangeException.ThrowIfGreaterThan(errList.Count, 0);

        this.InvokeStatusChangedEvent("Optifine 安装完成", ProgressValue.Finished);

        return id;

        void LogReceivedEvent(object sender, DataReceivedEventArgs args)
        {
            this.InvokeStatusChangedEvent(args.Data ?? "loading...", ProgressValue.FromDisplay(85));
        }
    }
}