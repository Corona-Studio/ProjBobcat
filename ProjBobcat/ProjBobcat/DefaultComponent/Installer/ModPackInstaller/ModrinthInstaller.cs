using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Modrinth;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class ModrinthInstaller : ModPackInstallerBase, IModrinthInstaller
{
    public string? GameId { get; init; }
    public override string RootPath { get; init; } = string.Empty;
    public required string ModPackPath { get; init; }

    public async Task<ModrinthModPackIndexModel?> ReadIndexTask()
    {
        using var archive = ArchiveFactory.Open(Path.GetFullPath(this.ModPackPath));
        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.Key?.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (manifestEntry == default)
            return default;

        await using var stream = manifestEntry.OpenEntryStream();

        var manifestModel = await JsonSerializer.DeserializeAsync(stream,
            ModrinthModPackIndexModelContext.Default.ModrinthModPackIndexModel);

        return manifestModel;
    }

    public void Install()
    {
        this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(this.GameId))
            throw new ArgumentNullException(nameof(this.GameId));
        if (string.IsNullOrEmpty(this.RootPath))
            throw new ArgumentNullException(nameof(this.RootPath));

        this.InvokeStatusChangedEvent("开始安装", 0);

        var index = await this.ReadIndexTask();

        if (index == default)
            throw new Exception("无法读取到 Modrinth 的 manifest 文件");

        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));
        var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

        var di = new DirectoryInfo(downloadPath);

        if (!di.Exists)
            di.Create();

        var downloadFiles = new List<DownloadFile>();

        foreach (var file in index.Files)
        {
            if (string.IsNullOrEmpty(file.Path)) continue;
            if (file.Downloads.Length == 0) continue;

            var fullPath = Path.Combine(idPath, file.Path);
            var downloadDir = Path.GetDirectoryName(fullPath)!;
            var fileName = Path.GetFileName(fullPath);
            var checkSum = file.Hashes.TryGetValue("sha1", out var sha1) ? sha1 : string.Empty;

            var df = new DownloadFile
            {
                CheckSum = checkSum,
                DownloadPath = downloadDir,
                DownloadUri = Random.Shared.GetItems(file.Downloads, 1)[0],
                FileName = fileName,
                FileSize = file.Size
            };
            df.Completed += this.WhenCompleted;

            downloadFiles.Add(df);
        }

        this.TotalDownloaded = 0;
        this.NeedToDownload = downloadFiles.Count;
        await DownloadHelper.AdvancedDownloadListFile(downloadFiles, new DownloadSettings
        {
            DownloadParts = 8,
            RetryCount = 10,
            Timeout = TimeSpan.FromMinutes(1),
            CheckFile = true,
            HashType = HashType.SHA1
        });

        if (!this.FailedFiles.IsEmpty)
            throw new NullReferenceException("未能下载全部的 Mods");

        using var archive = ArchiveFactory.Open(Path.GetFullPath(this.ModPackPath));

        this.TotalDownloaded = 0;
        this.NeedToDownload = archive.Entries.Count();

        const string decompressPrefix = "overrides";

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Key)) continue;
            if (!entry.Key.StartsWith(decompressPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.Key[(decompressPrefix.Length + 1)..];
            if (string.IsNullOrEmpty(subPath)) continue;

            var path = Path.Combine(Path.GetFullPath(idPath), subPath);
            var dirPath = Path.GetDirectoryName(path)!;

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            if (entry.IsDirectory)
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                continue;
            }

            var subPathLength = subPath.Length;
            var subPathName = subPathLength > 35
                ? $"...{subPath[(subPathLength - 15)..]}"
                : subPath;

            this.InvokeStatusChangedEvent($"解压缩安装文件：{subPathName}",
                (double)this.TotalDownloaded / this.NeedToDownload * 100);

            await using var fs = File.OpenWrite(path);
            entry.WriteTo(fs);

            this.TotalDownloaded++;
        }

        this.InvokeStatusChangedEvent("安装完成", 100);
    }
}