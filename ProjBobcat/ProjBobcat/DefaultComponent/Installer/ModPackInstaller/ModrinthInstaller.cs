using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Modrinth;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class ModrinthInstaller : ModPackInstallerBase, IModrinthInstaller
{
    public string GameId { get; set; }
    public string ModPackPath { get; set; }

    public async Task<ModrinthModPackIndexModel?> ReadIndexTask()
    {
        using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));
        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.Key.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry == default)
            return default;

        await using var stream = manifestEntry.OpenEntryStream();

        var manifestModel = await JsonSerializer.DeserializeAsync(stream,
            ModrinthModPackIndexModelContext.Default.ModrinthModPackIndexModel);

        return manifestModel;
    }

    public void Install()
    {
        InstallTaskAsync().Wait();
    }

    public async Task InstallTaskAsync()
    {
        InvokeStatusChangedEvent("开始安装", 0);

        var index = await ReadIndexTask();

        if (index == default)
            throw new Exception("无法读取到 Modrinth 的 manifest 文件");

        var idPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(GameId));
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
            var downloadDir = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);
            var checkSum = file.Hashes.TryGetValue("sha1", out var sha1) ? sha1 : string.Empty;

            var df = new DownloadFile
            {
                CheckSum = checkSum,
                DownloadPath = downloadDir,
                DownloadUri = file.Downloads.RandomSample(),
                FileName = fileName,
                FileSize = file.Size
            };
            df.Completed += WhenCompleted;

            downloadFiles.Add(df);
        }

        TotalDownloaded = 0;
        NeedToDownload = downloadFiles.Count;
        await DownloadHelper.AdvancedDownloadListFile(downloadFiles, new DownloadSettings
        {
            DownloadParts = 4,
            RetryCount = 10,
            Timeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds,
            CheckFile = true,
            HashType = HashType.SHA1
        });

        if (!FailedFiles.IsEmpty)
            throw new NullReferenceException("未能下载全部的 Mods");

        using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));

        TotalDownloaded = 0;
        NeedToDownload = archive.Entries.Count();

        const string decompressPrefix = "overrides";

        foreach (var entry in archive.Entries)
        {
            if (!entry.Key.StartsWith(decompressPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.Key[(decompressPrefix.Length + 1)..];
            if (string.IsNullOrEmpty(subPath)) continue;

            var path = Path.Combine(Path.GetFullPath(idPath), subPath);
            var dirPath = Path.GetDirectoryName(path);

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

            InvokeStatusChangedEvent($"解压缩安装文件：{subPathName}", (double)TotalDownloaded / NeedToDownload * 100);

            await using var fs = File.OpenWrite(path);
            entry.WriteTo(fs);

            TotalDownloaded++;
        }

        InvokeStatusChangedEvent("安装完成", 100);
    }
}