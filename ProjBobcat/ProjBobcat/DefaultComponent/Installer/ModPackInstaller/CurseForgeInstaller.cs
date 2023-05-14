using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class CurseForgeInstaller : ModPackInstallerBase, ICurseForgeInstaller
{
    public string ModPackPath { get; set; }
    public string GameId { get; set; }

    public void Install()
    {
        InstallTaskAsync().Wait();
    }

    public async Task InstallTaskAsync()
    {
        InvokeStatusChangedEvent("开始安装", 0);

        var manifest = await ReadManifestTask();

        if (manifest == default)
            throw new Exception("无法读取到 CurseForge 的 manifest 文件");

        var idPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(GameId));
        var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

        var di = new DirectoryInfo(downloadPath);

        if (!di.Exists)
            di.Create();

        NeedToDownload = manifest.Files.Length;

        var urlBlock = new TransformManyBlock<IEnumerable<CurseForgeFileModel>, (long, long)>(urls =>
        {
            return urls.Select(file => (file.ProjectId, file.FileId));
        });

        var urlBags = new ConcurrentBag<DownloadFile>();
        var actionBlock = new ActionBlock<(long, long)>(async t =>
        {
            var downloadUrlRes = await CurseForgeAPIHelper.GetAddonDownloadUrl(t.Item1, t.Item2);
            var d = downloadUrlRes.Trim('"');
            var fn = Path.GetFileName(d);

            var downloadFile = new DownloadFile
            {
                DownloadPath = di.FullName,
                DownloadUri = d,
                FileName = fn
            };
            downloadFile.Completed += WhenCompleted;

            urlBags.Add(downloadFile);

            TotalDownloaded++;

            var progress = (double)TotalDownloaded / NeedToDownload * 100;

            InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                progress);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 32,
            MaxDegreeOfParallelism = 32
        });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        urlBlock.LinkTo(actionBlock, linkOptions);
        urlBlock.Post(manifest.Files);
        urlBlock.Complete();

        await actionBlock.Completion;

        TotalDownloaded = 0;
        await DownloadHelper.AdvancedDownloadListFile(urlBags, new DownloadSettings
        {
            DownloadParts = 4,
            RetryCount = 10,
            Timeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds
        });

        if (!FailedFiles.IsEmpty)
            throw new Exception("未能下载全部的 Mods");

        using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));

        TotalDownloaded = 0;
        NeedToDownload = archive.Entries.Count();

        foreach (var entry in archive.Entries)
        {
            if (!entry.Key.StartsWith(manifest.Overrides, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.Key[(manifest.Overrides.Length + 1)..];
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

    public async Task<CurseForgeManifestModel?> ReadManifestTask()
    {
        using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));
        var manifestEntry =
            archive.Entries.FirstOrDefault(x => x.Key.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry == default)
            return default;

        await using var stream = manifestEntry.OpenEntryStream();

        var manifestModel =
            await JsonSerializer.DeserializeAsync(stream,
                CurseForgeManifestModelContext.Default.CurseForgeManifestModel);

        return manifestModel;
    }
}