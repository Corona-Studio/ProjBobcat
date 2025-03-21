﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;
using ProjBobcat.Interface.Services;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class CurseForgeInstaller : ModPackInstallerBase, ICurseForgeInstaller
{
    public override string RootPath { get; init; } = string.Empty;
    public required string ModPackPath { get; init; }
    public string? GameId { get; init; }
    public required ICurseForgeApiService CurseForgeApiService { get; init; }

    public void Install()
    {
        this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task InstallTaskAsync()
    {
        ArgumentException.ThrowIfNullOrEmpty(this.GameId);
        ArgumentException.ThrowIfNullOrEmpty(this.RootPath);

        this.InvokeStatusChangedEvent("开始安装", ProgressValue.Start);

        var manifest = await ReadManifestTask(ModPackPath);

        ArgumentNullException.ThrowIfNull(manifest, "无法读取到 CurseForge 的 manifest 文件");

        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));
        var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

        var di = new DirectoryInfo(downloadPath);

        if (!di.Exists)
            di.Create();

        this.NeedToDownload = manifest.Files?.Length ?? 0;

        var urlBlock = new TransformManyBlock<IEnumerable<CurseForgeFileModel>, (long, long)>(urls =>
        {
            return urls.Select(file => (file.ProjectId, file.FileId));
        });

        var urlBags = new ConcurrentBag<SimpleDownloadFile>();
        var urlReqExceptions = new ConcurrentBag<CurseForgeModResolveException>();
        var actionBlock = new ActionBlock<(long, long)>(async t =>
        {
            try
            {
                var downloadUrlRes = await CurseForgeApiService.GetAddonDownloadUrl(t.Item1, t.Item2);

                if (string.IsNullOrEmpty(downloadUrlRes))
                    throw new CurseForgeModResolveException(t.Item1, t.Item2);

                var d = downloadUrlRes.Trim('"');
                var fn = Path.GetFileName(d);

                var downloadFile = new SimpleDownloadFile
                {
                    DownloadPath = di.FullName,
                    DownloadUri = d,
                    FileName = fn
                };
                downloadFile.Completed += this.WhenCompleted;

                urlBags.Add(downloadFile);

                var addedTotalDownloaded = Interlocked.Increment(ref this.TotalDownloaded);
                var addedNeedToDownload = Interlocked.Increment(ref this.NeedToDownload);
                var progress = ProgressValue.Create(addedTotalDownloaded, addedNeedToDownload);

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);
            }
            catch (CurseForgeModResolveException e)
            {
                this.InvokeStatusChangedEvent(
                    $"MOD [{t.Item1}] 的下载地址解析失败，尝试手动拼接",
                    ProgressValue.FromDisplay(50));

                var (guessed, df) = await this.TryGuessModDownloadLink(t.Item2, di.FullName);

                if (!guessed || df == null)
                {
                    try
                    {
                        var info = await CurseForgeApiService.GetAddon(t.Item1);

                        ArgumentNullException.ThrowIfNull(info);

                        var moreInfo = $"""
                                        模组名称：{info.Name}
                                        模组链接：{(info.Links?.TryGetValue("websiteUrl", out var link) ?? false ? link : "-")}
                                        """;
                        var ex = new CurseForgeModResolveException(t.Item1, t.Item2, moreInfo);

                        urlReqExceptions.Add(ex);
                    }
                    catch
                    {
                        urlReqExceptions.Add(e);
                    }

                    return;
                }

                urlBags.Add(df);

                var addedTotalDownloaded = Interlocked.Increment(ref this.TotalDownloaded);
                var addedNeedToDownload = Interlocked.Increment(ref this.NeedToDownload);
                var progress = ProgressValue.Create(addedTotalDownloaded, addedNeedToDownload);

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 32,
            MaxDegreeOfParallelism = 32
        });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        urlBlock.LinkTo(actionBlock, linkOptions);
        urlBlock.Post(manifest.Files ?? []);
        urlBlock.Complete();

        await actionBlock.Completion;

        ArgumentOutOfRangeException.ThrowIfEqual(urlReqExceptions.IsEmpty, false);

        this.TotalDownloaded = 0;
        await DownloadHelper.AdvancedDownloadListFile(urlBags, new DownloadSettings
        {
            DownloadParts = 8,
            RetryCount = 10,
            Timeout = TimeSpan.FromMinutes(1),
            HttpClientFactory = HttpClientFactory
        });

        ArgumentOutOfRangeException.ThrowIfEqual(this.FailedFiles.IsEmpty, false);

        var modPackFullPath = Path.GetFullPath(this.ModPackPath);

        await using var modPackFs = File.OpenRead(modPackFullPath);
        using var archive = new ZipArchive(modPackFs, ZipArchiveMode.Read);

        this.TotalDownloaded = 0;
        this.NeedToDownload = archive.Entries.Count;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(manifest.Overrides) ||
                !entry.FullName.StartsWith(manifest.Overrides, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.FullName[(manifest.Overrides.Length + 1)..];
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

    public static async Task<CurseForgeManifestModel?> ReadManifestTask(string modPackPath)
    {
        var modPackFullPath = Path.GetFullPath(modPackPath);

        await using var fullPackFs = File.OpenRead(modPackFullPath);
        using var archive = new ZipArchive(fullPackFs, ZipArchiveMode.Read);

        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry == null)
            return null;

        await using var stream = manifestEntry.Open();

        var manifestModel =
            await JsonSerializer.DeserializeAsync(stream,
                CurseForgeManifestModelContext.Default.CurseForgeManifestModel);

        return manifestModel;
    }

    public static async Task<(string? FileName, string? Url)> TryGuessModDownloadLink(
        ICurseForgeApiService curseForgeApiService,
        IHttpClientFactory httpClientFactory,
        long fileId)
    {
        try
        {
            var files = await curseForgeApiService.GetFiles([fileId]);

            if (files == null || files.Length == 0) return default;

            var file = files.FirstOrDefault(f => f.Id == fileId);

            if (file == null || string.IsNullOrEmpty(file.FileName)) return default;

            var fileName = file.FileName;
            var fileIdStr = fileId.ToString();
            var pendingCheckUrls = new[]
            {
                $"https://edge.forgecdn.net/files/{fileIdStr[..4]}/{fileIdStr[4..]}/{fileName}",
                $"https://mediafiles.forgecdn.net/files/{fileIdStr[..4]}/{fileIdStr[4..]}/{fileName}"
            };

            var client = httpClientFactory.CreateClient();

            foreach (var url in pendingCheckUrls)
            {
                using var checkReq = new HttpRequestMessage(HttpMethod.Head, url);
                using var checkRes = await client.SendAsync(checkReq);

                if (!checkRes.IsSuccessStatusCode) continue;

                return (fileName, url);
            }

            return default;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return default;
        }
    }

    async Task<(bool, SimpleDownloadFile?)> TryGuessModDownloadLink(long fileId, string downloadPath)
    {
        var pair = await TryGuessModDownloadLink(CurseForgeApiService, HttpClientFactory, fileId);

        if (string.IsNullOrEmpty(pair.FileName) || string.IsNullOrEmpty(pair.Url)) return (false, null);

        var df = new SimpleDownloadFile
        {
            DownloadPath = downloadPath,
            DownloadUri = pair.Url,
            FileName = pair.FileName
        };

        df.Completed += this.WhenCompleted;

        return (true, df);
    }
}