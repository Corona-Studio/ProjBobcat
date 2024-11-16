using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;
using SharpCompress.Archives;
using VersionInfo = ProjBobcat.Class.Model.Forge.VersionInfo;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller;

public class LegacyForgeInstaller : InstallerBase, IForgeInstaller
{
    public required string ForgeVersion { get; init; }
    public required string ForgeExecutablePath { get; init; }
    public override required string RootPath { get; init; }

    public VersionLocatorBase VersionLocator
    {
        get => throw new NotImplementedException();
        init => throw new NotImplementedException();
    }

    public string DownloadUrlRoot
    {
        get => throw new NotImplementedException();
        init => throw new NotImplementedException();
    }

    public ForgeInstallResult InstallForge()
    {
        return this.InstallForgeTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<ForgeInstallResult> InstallForgeTaskAsync()
    {
        try
        {
            this.InvokeStatusChangedEvent("解压安装文件", 0.05);

            using var reader = ArchiveFactory.Open(this.ForgeExecutablePath);
            var profileEntry =
                reader.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("install_profile.json", StringComparison.Ordinal) ?? false);
            var legacyJarEntry =
                reader.Entries.FirstOrDefault(e =>
                    e.Key?.Equals($"forge-{this.ForgeVersion}-universal.jar", StringComparison.OrdinalIgnoreCase) ??
                    false);

            if (profileEntry == default)
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Cause = "未找到 install_profile.json",
                        Error = "未找到 install_profile.json",
                        ErrorMessage = "未找到 install_profile.json"
                    },
                    Succeeded = false
                };

            if (legacyJarEntry == default)
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Cause = "未找到 Forge Jar",
                        Error = "未找到 Forge Jar",
                        ErrorMessage = "未找到 Forge Jar"
                    },
                    Succeeded = false
                };

            this.InvokeStatusChangedEvent("解压完成", 0.1);

            await using var stream = profileEntry.OpenEntryStream();

            this.InvokeStatusChangedEvent("解析安装文档", 0.35);

            var profileModel = await JsonSerializer.DeserializeAsync(stream,
                LegacyForgeInstallProfileContext.Default.LegacyForgeInstallProfile);
            if (profileModel == null)
                throw new ArgumentNullException(nameof(profileModel));

            this.InvokeStatusChangedEvent("解析完成", 0.75);

            var id = string.IsNullOrEmpty(this.CustomId) ? profileModel.VersionInfo.Id : this.CustomId;

            var installDir = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));
            var jsonPath = GamePathHelper.GetGameJsonPath(this.RootPath, id);

            var forgeDi = new DirectoryInfo(installDir);
            if (!forgeDi.Exists)
                forgeDi.Create();

            profileModel.VersionInfo.Id = id;
            if (!string.IsNullOrEmpty(this.InheritsFrom))
                profileModel.VersionInfo.InheritsFrom = this.InheritsFrom;

            var forgeLibrary = profileModel.VersionInfo.Libraries.First(l =>
                l.Name.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase));
            var mavenInfo = forgeLibrary.Name.ResolveMavenString()!;

            var libSubPath = GamePathHelper.GetLibraryPath(mavenInfo.Path);
            var forgeLibPath = Path.Combine(this.RootPath, libSubPath);

            var libDi = new DirectoryInfo(Path.GetDirectoryName(forgeLibPath)!);

            if (!libDi.Exists)
                libDi.Create();

            await using var fs = File.OpenWrite(forgeLibPath);
            legacyJarEntry.WriteTo(fs);

            var versionJsonString = JsonSerializer.Serialize(profileModel.VersionInfo, typeof(VersionInfo),
                new LegacyForgeInstallVersionInfoContext(JsonHelper.CamelCasePropertyNamesSettings()));

            await File.WriteAllTextAsync(jsonPath, versionJsonString);
            this.InvokeStatusChangedEvent("文件写入完成", 1);

            return new ForgeInstallResult
            {
                Succeeded = true
            };
        }
        catch (Exception ex)
        {
            return new ForgeInstallResult
            {
                Error = new ErrorModel
                {
                    Error = "安装失败",
                    Exception = ex
                },
                Succeeded = false
            };
        }
    }
}