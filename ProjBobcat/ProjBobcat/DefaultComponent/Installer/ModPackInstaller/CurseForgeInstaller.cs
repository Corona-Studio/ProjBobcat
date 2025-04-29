using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public record ResolvedUrlRecord(long ProjectId, long FileId, string Url);

[JsonSerializable(typeof(IReadOnlyDictionary<string, ResolvedUrlRecord>))]
public partial class CurseForgeCacheJsonContext : JsonSerializerContext;

public sealed class CurseForgeInstaller : ModPackInstallerBase, ICurseForgeInstaller
{
    public required int RetryCount { get; init; } = 6;
    public required bool FreshInstall { get; init; }
    public required ICurseForgeApiService CurseForgeApiService { get; init; }
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

        this.InvokeStatusChangedEvent("开始安装", ProgressValue.Start);

        var manifest = await ReadManifestTask(this.ModPackPath);

        ArgumentNullException.ThrowIfNull(manifest, "无法读取到 CurseForge 的 manifest 文件");

        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));
        var resolveUrlPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId), ".resolved");
        var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

        if (this.FreshInstall) FileHelper.DeleteFileWithRetry(resolveUrlPath);

        var di = new DirectoryInfo(downloadPath);

        if (!di.Exists)
            di.Create();

        this.NeedToDownload = manifest.Files?.Length ?? 0;

        var urlBlock = new TransformManyBlock<IEnumerable<CurseForgeFileModel>, (long, long)>(urls =>
        {
            return urls.Select(file => (file.ProjectId, file.FileId));
        });

        var tempResolvedUrl = await TryReadUrlCacheAsync(resolveUrlPath);
        var resolvedUrl = new ConcurrentDictionary<string, ResolvedUrlRecord>(tempResolvedUrl);

        var urlBags = new ConcurrentBag<SimpleDownloadFile>();
        var urlReqExceptions = new ConcurrentBag<CurseForgeModResolveException>();
        var lastProgress = ProgressValue.Start;
        var actionBlock = new ActionBlock<(long, long)>(async t =>
        {
            var key = $"{t.Item1:####}{t.Item2:####}";

            if (resolvedUrl.TryGetValue(key, out var value) &&
                !string.IsNullOrEmpty(value.Url))
            {
                var d = value.Url;
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
                var progress = ProgressValue.Create(addedTotalDownloaded, this.NeedToDownload);
                lastProgress = progress;

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);

                return;
            }

            try
            {
                string? downloadUrlRes = null;

                for (var i = 0; i < this.RetryCount; i++)
                    try
                    {
                        downloadUrlRes = await this.CurseForgeApiService.GetAddonDownloadUrl(t.Item1, t.Item2);
                        break;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        downloadUrlRes = null;

                        if (i == this.RetryCount - 1)
                            throw new CurseForgeModResolveException(t.Item1, t.Item2, e);
                    }

                if (string.IsNullOrEmpty(downloadUrlRes))
                    throw new CurseForgeModResolveException(t.Item1, t.Item2);

                var d = downloadUrlRes.Trim('"');
                var urlRecord = new ResolvedUrlRecord(t.Item2, t.Item1, d);
                resolvedUrl.AddOrUpdate(key, _ => urlRecord, (_, _) => urlRecord);

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
                var progress = ProgressValue.Create(addedTotalDownloaded, this.NeedToDownload);
                lastProgress = progress;

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);
            }
            catch (CurseForgeModResolveException e)
            {
                this.InvokeStatusChangedEvent(
                    $"MOD [{t.Item1}] 的下载地址解析失败，尝试手动拼接",
                    lastProgress);

                (bool, SimpleDownloadFile?) pair = (false, null);

                for (var i = 0; i < this.RetryCount; i++)
                    try
                    {
                        pair = await this.TryGuessModDownloadLink(t.Item2, di.FullName);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);

                        if (i == this.RetryCount - 1)
                            throw;
                    }

                var (guessed, df) = pair;

                if (!guessed || df == null)
                {
                    try
                    {
                        var info = await this.CurseForgeApiService.GetAddon(t.Item1);

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

                var urlRecord = new ResolvedUrlRecord(t.Item2, t.Item1, df.DownloadUri);
                resolvedUrl.AddOrUpdate(key, _ => urlRecord, (_, _) => urlRecord);

                var addedTotalDownloaded = Interlocked.Increment(ref this.TotalDownloaded);
                var progress = ProgressValue.Create(addedTotalDownloaded, this.NeedToDownload);

                this.InvokeStatusChangedEvent($"成功解析 MOD [{t.Item1}] 的下载地址",
                    progress);
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 32
        });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        urlBlock.LinkTo(actionBlock, linkOptions);
        urlBlock.Post(manifest.Files ?? []);
        urlBlock.Complete();

        await actionBlock.Completion;

        try
        {
            await using var fs = File.Create(resolveUrlPath);
            await JsonSerializer.SerializeAsync(fs, resolvedUrl,
                CurseForgeCacheJsonContext.Default.IReadOnlyDictionaryStringResolvedUrlRecord);
        }
        catch
        {
            // Ignore
        }

        if (!urlReqExceptions.IsEmpty)
            throw new AggregateException(urlReqExceptions);

        this.TotalDownloaded = 0;
        await DownloadHelper.AdvancedDownloadListFile(urlBags, new DownloadSettings
        {
            DownloadParts = 8,
            RetryCount = 10,
            Timeout = TimeSpan.FromMinutes(1),
            HttpClientFactory = this.HttpClientFactory
        });

        ArgumentOutOfRangeException.ThrowIfEqual(this.FailedFiles.IsEmpty, false);

        var modPackFullPath = Path.GetFullPath(this.ModPackPath);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");

        await using var modPackFs = File.OpenRead(modPackFullPath);
        using var archive = new ZipArchive(modPackFs, ZipArchiveMode.Read, true, gbk);

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

    private static async Task<IReadOnlyDictionary<string, ResolvedUrlRecord>> TryReadUrlCacheAsync(string path)
    {
        try
        {
            await using var fs = File.OpenRead(path);
            var result = await JsonSerializer.DeserializeAsync(
                fs,
                CurseForgeCacheJsonContext.Default.IReadOnlyDictionaryStringResolvedUrlRecord);

            return result ?? ImmutableDictionary<string, ResolvedUrlRecord>.Empty;
        }
        catch (Exception)
        {
            return ImmutableDictionary<string, ResolvedUrlRecord>.Empty;
        }
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
        var pair = await TryGuessModDownloadLink(this.CurseForgeApiService, this.HttpClientFactory, fileId);

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