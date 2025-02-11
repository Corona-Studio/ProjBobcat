﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Modrinth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class ModrinthInstaller : ModPackInstallerBase, IModrinthInstaller
{
    public string? GameId { get; init; }
    public override string RootPath { get; init; } = string.Empty;
    public required string ModPackPath { get; init; }

    public async Task<ModrinthModPackIndexModel?> ReadIndexTask()
    {
        var path = Path.GetFullPath(this.ModPackPath);

        await using var fs = File.OpenRead(path);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.FullName.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry == null)
            return null;

        await using var stream = manifestEntry.Open();

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
        ArgumentException.ThrowIfNullOrEmpty(this.GameId);
        ArgumentException.ThrowIfNullOrEmpty(this.RootPath);

        this.InvokeStatusChangedEvent("开始安装", ProgressValue.Start);

        var index = await this.ReadIndexTask() ?? throw new Exception("无法读取到 Modrinth 的 manifest 文件");
        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));
        var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

        var di = new DirectoryInfo(downloadPath);

        if (!di.Exists)
            di.Create();

        var downloadFiles = new List<SimpleDownloadFile>();

        foreach (var file in index.Files)
        {
            if (string.IsNullOrEmpty(file.Path)) continue;
            if (file.Downloads.Length == 0) continue;

            var fullPath = Path.Combine(idPath, file.Path);
            var downloadDir = Path.GetDirectoryName(fullPath)!;
            var fileName = Path.GetFileName(fullPath);
            var checkSum = file.Hashes.TryGetValue("sha1", out var sha1) ? sha1 : string.Empty;

            var df = new SimpleDownloadFile
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

        ArgumentOutOfRangeException.ThrowIfEqual(this.FailedFiles.IsEmpty, false);

        var modPackFullPath = Path.GetFullPath(this.ModPackPath);

        await using var modPackFs = File.OpenRead(modPackFullPath);
        using var archive = new ZipArchive(modPackFs, ZipArchiveMode.Read);

        this.TotalDownloaded = 0;
        this.NeedToDownload = archive.Entries.Count;

        const string decompressPrefix = "overrides";

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(decompressPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.FullName[(decompressPrefix.Length + 1)..];
            if (string.IsNullOrEmpty(subPath)) continue;

            var path = Path.Combine(Path.GetFullPath(idPath), subPath);
            var dirPath = Path.GetDirectoryName(path)!;

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            if (entry.IsDirectory())
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                continue;
            }

            var subPathLength = subPath.Length;
            var subPathName = subPathLength > 35
                ? $"...{subPath[(subPathLength - 15)..]}"
                : subPath;

            var progress = ProgressValue.Create(this.TotalDownloaded, this.NeedToDownload);

            this.InvokeStatusChangedEvent($"解压缩安装文件：{subPathName}", progress);

            await using var fs = File.OpenWrite(path);
            await using var entryStream = entry.Open();

            await entryStream.CopyToAsync(fs);

            this.TotalDownloaded++;
        }

        this.InvokeStatusChangedEvent("安装完成", ProgressValue.Finished);
    }
}