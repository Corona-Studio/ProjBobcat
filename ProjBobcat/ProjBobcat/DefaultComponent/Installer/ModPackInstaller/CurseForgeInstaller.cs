using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public sealed class CurseForgeInstaller : ModPackInstallerBase, ICurseForgeInstaller
{
    public override string RootPath { get; init; } = string.Empty;
    public required string ModPackPath { get; init; }
    public string? GameId { get; init; }

    public void Install()
    {
        this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task InstallTaskAsync()
    {
        ArgumentException.ThrowIfNullOrEmpty(this.GameId);
        ArgumentException.ThrowIfNullOrEmpty(this.RootPath);

        this.InvokeStatusChangedEvent("开始安装", 0);

        var manifest = await this.ReadManifestTask();

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

        var urlBags = new ConcurrentBag<DownloadFile>();
        var urlReqExceptions = new ConcurrentBag<CurseForgeModResolveException>();
        var actionBlock = new ActionBlock<(long, long)>(async t =>
        {
            try
            {
                var downloadUrlRes = await CurseForgeAPIHelper.GetAddonDownloadUrl(t.Item1, t.Item2);

                if (string.IsNullOrEmpty(downloadUrlRes))
                    throw new CurseForgeModResolveException(t.Item1, t.Item2);

                var d = downloadUrlRes.Trim('"');
                var fn = Path.GetFileName(d);

                var downloadFile = new DownloadFile
                {
                    DownloadPath = di.FullName,
                    DownloadUri = d,
                    FileName = fn
                };
                downloadFile.Completed += this.WhenCompleted;

                urlBags.Add(downloadFile);

                this.TotalDownloaded++;
                this.NeedToDownload++;

                var progress = (double)this.TotalDownloaded / this.NeedToDownload * 100;

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);
            }
            catch (CurseForgeModResolveException e)
            {
                this.InvokeStatusChangedEvent($"MOD [{t.Item1}] 的下载地址解析失败，尝试手动拼接",
                    114514);

                var (guessed, df) = await this.TryGuessModDownloadLink(t.Item2, di.FullName);

                if (!guessed || df == null)
                {
                    try
                    {
                        var info = await CurseForgeAPIHelper.GetAddon(t.Item1);

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

                this.TotalDownloaded++;
                this.NeedToDownload++;

                var progress = (double)this.TotalDownloaded / this.NeedToDownload * 100;

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

        if (!urlReqExceptions.IsEmpty)
            throw new AggregateException(urlReqExceptions);

        this.TotalDownloaded = 0;
        await DownloadHelper.AdvancedDownloadListFile(urlBags, new DownloadSettings
        {
            DownloadParts = 8,
            RetryCount = 10,
            Timeout = TimeSpan.FromMinutes(1)
        });

        if (!this.FailedFiles.IsEmpty)
            throw new Exception("未能下载全部的 Mods");

        using var archive = ArchiveFactory.Open(Path.GetFullPath(this.ModPackPath));

        this.TotalDownloaded = 0;
        this.NeedToDownload = archive.Entries.Count();

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Key)) continue;
            if (string.IsNullOrEmpty(manifest.Overrides) ||
                !entry.Key.StartsWith(manifest.Overrides, StringComparison.OrdinalIgnoreCase)) continue;

            var subPath = entry.Key[(manifest.Overrides.Length + 1)..];
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

    public async Task<CurseForgeManifestModel?> ReadManifestTask()
    {
        using var archive = ArchiveFactory.Open(Path.GetFullPath(this.ModPackPath));
        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.Key?.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (manifestEntry == default)
            return default;

        await using var stream = manifestEntry.OpenEntryStream();

        var manifestModel =
            await JsonSerializer.DeserializeAsync(stream,
                CurseForgeManifestModelContext.Default.CurseForgeManifestModel);

        return manifestModel;
    }

    public static async Task<(string? FileName, string? Url)> TryGuessModDownloadLink(long fileId)
    {
        try
        {
            var files = await CurseForgeAPIHelper.GetFiles([fileId]);

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

            var httpClient = HttpClientHelper.DefaultClient;

            foreach (var url in pendingCheckUrls)
            {
                using var checkReq = new HttpRequestMessage(HttpMethod.Head, url);
                using var checkRes = await httpClient.SendAsync(checkReq);

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

    async Task<(bool, DownloadFile?)> TryGuessModDownloadLink(long fileId, string downloadPath)
    {
        var pair = await TryGuessModDownloadLink(fileId);

        if (string.IsNullOrEmpty(pair.FileName) || string.IsNullOrEmpty(pair.Url)) return (false, null);

        var df = new DownloadFile
        {
            DownloadPath = downloadPath,
            DownloadUri = pair.Url,
            FileName = pair.FileName
        };

        df.Completed += this.WhenCompleted;

        return (true, df);
    }
}