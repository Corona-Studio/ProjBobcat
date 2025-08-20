using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
    public required int RetryCount { get; init; } = 6;
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

        this.NeedToDownload = manifest.Files?.Length ?? 0;

        var fileIds = manifest.Files
            ?.Where(file => file.ProjectId != 0 && file.FileId != 0)
            .Select(file => file.FileId)
            .ToArray() ?? [];

        CurseForgeLatestFileModel[]? files = null;

        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                files = await GetModPackFiles(this.CurseForgeApiService, fileIds);
                break;
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        ArgumentNullException.ThrowIfNull(files, "无法获取 CurseForge 的文件列表");

        var missingFileIds = fileIds.Except(files.Select(file => file.Id)).ToArray();

        // Fetch missing files if any
        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                files =
                [
                    ..files,
                    .. await GetModPackFiles(this.CurseForgeApiService, missingFileIds, true)
                ];
                break;
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        var projectIds = manifest.Files
            ?.Where(file => file.ProjectId != 0 && file.FileId != 0)
            .Select(file => file.ProjectId)
            .ToArray() ?? [];

        CurseForgeAddonInfo[]? modProjectDetails = null;

        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                modProjectDetails = await GetModProjectDetails(this.CurseForgeApiService, projectIds);
                break;
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        ArgumentNullException.ThrowIfNull(modProjectDetails, "无法获取 CurseForge 的模组列表");
        ArgumentOutOfRangeException.ThrowIfLessThan(fileIds.Length, files.Length);

        var missingProjectIds = projectIds.Except(modProjectDetails.Select(mod => mod.Id)).ToArray();

        // Fetch missing projects if any
        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                modProjectDetails =
                [
                    ..modProjectDetails,
                    .. await GetModProjectDetails(this.CurseForgeApiService, missingProjectIds, true)
                ];
                break;
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        var fileDic = files.ToDictionary(k => k.Id, v => v);
        var projectDic = modProjectDetails.ToDictionary(k => k.Id, v => v);
        var downloadFiles = new List<MultiSourceDownloadFile>();

        foreach (var fileId in fileIds)
        {
            var file = fileDic.GetValueOrDefault(fileId);
            var mod = projectDic.GetValueOrDefault(file?.ProjectId ?? 0);

            if (file == null) continue;

            string? downloadPath = null;

            if (mod != null)
                downloadPath = GetResourceFolderName(mod.PrimaryCategoryId);
            if (string.IsNullOrEmpty(downloadPath))
                downloadPath = file.FileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    ? "mods"
                    : file.Modules?.Any(f => f.FolderName == "META-INF") ?? false
                        ? "mods"
                        : "resourcepacks";

            var fullDownloadPath = Path.Combine(Path.GetFullPath(idPath), downloadPath);
            var di = new DirectoryInfo(fullDownloadPath);

            if (!di.Exists)
                di.Create();

            var downloadUrl = file.DownloadUrl;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                var fallbackUrls = GeneratePossibleDownloadUrls(file.Id, file.FileName);
                var proceededUrls = DownloadUriReplacer == null
                    ? GeneratePossibleDownloadUrls(file.Id, file.FileName)
                    : [.. DownloadUriReplacer(GeneratePossibleDownloadUrls(file.Id, file.FileName)), ..fallbackUrls];

                var guessDownloadFile = new MultiSourceDownloadFile
                {
                    DownloadPath = di.FullName,
                    DownloadUris = proceededUrls.Distinct().Select(u => new DownloadUriInfo(u, 1)).ToArray(),
                    FileName = file.FileName
                };

                guessDownloadFile.Completed += this.WhenCompleted;
                downloadFiles.Add(guessDownloadFile);
                continue;
            }

            IEnumerable<string> urls = DownloadUriReplacer == null
                ? [downloadUrl]
                : [.. DownloadUriReplacer([downloadUrl]), downloadUrl];

            var downloadFile = new MultiSourceDownloadFile
            {
                DownloadPath = di.FullName,
                DownloadUris = urls.Distinct().Select(u => new DownloadUriInfo(u, 1)).ToArray(),
                FileName = file.FileName
            };

            downloadFile.Completed += this.WhenCompleted;
            downloadFiles.Add(downloadFile);
        }

        this.InvokeStatusChangedEvent("成功解析整合包模组的下载地址", ProgressValue.Finished);

        this.TotalDownloaded = 0;

        if (downloadFiles.Count > 0)
        {
            await DownloadHelper.AdvancedDownloadListFile(downloadFiles, new DownloadSettings
            {
                DownloadParts = 2,
                RetryCount = downloadFiles.MaxBy(u => u.DownloadUris.Count)!.DownloadUris.Count,
                Timeout = TimeSpan.FromMinutes(5),
                HttpClientFactory = this.HttpClientFactory
            });
        }

        var modPackFullPath = Path.GetFullPath(this.ModPackPath);
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

        if (this.FailedFiles.IsEmpty &&
            TotalDownloaded == 0 &&
            downloadFiles.Count != 0)
        {
            throw new Exception(
                "我们无法下载这个整合包中的模组，这可能是因为您和 CurseForge 的网络连接不稳定导致的。尽管如此，我们还是完成了整合包安装的其他步骤。您可以稍后尝试重新安装或是手动下载整合包模组。");
        }

        if (!this.FailedFiles.IsEmpty)
        {
            var failedFileExList = new List<Exception>();

            foreach (var failedFile in this.FailedFiles)
            {
                var urls = failedFile switch
                {
                    SimpleDownloadFile sd => [sd.DownloadUri],
                    MultiSourceDownloadFile msd => msd.DownloadUris.Select(d => d.DownloadUri).ToArray(),
                    _ => throw new ArgumentOutOfRangeException(nameof(failedFile))
                };

                failedFileExList.Add(new Exception($"""
                                                    文件名：{failedFile.FileName}
                                                    下载链接：[{string.Join(',', urls)}]
                                                    """));
            }

            throw new AggregateException(
                "整合包已经成功安装，但是部分文件还是下载失败了。这不一定会影响游戏的启动。您可以选择稍后手动下载这些文件。",
                failedFileExList);
        }
    }

    private static readonly FrozenSet<long> ResourcePacksFilterIds =
#if NET9_0_OR_GREATER
        FrozenSet.Create<long>(4465, 5193, 5244);
#else
        new []{4465L, 5193L, 5244L}.ToFrozenSet();
#endif

    private static readonly FrozenSet<long> ModFilterIds =
#if NET9_0_OR_GREATER
        FrozenSet.Create<long>(
            4485, 4545, 4558,
            4671, 4672, 4773,
            4843, 4906, 5191,
            5232, 5299, 5314,
            6145, 6484, 6814,
            6821, 6954
        );
#else
        new []{4485L, 4545L, 4558L,
            4671L, 4672L, 4773L,
            4843L, 4906L, 5191L,
            5232L, 5299L, 5314L,
            6145L, 6484L, 6814L,
            6821L, 6954L}.ToFrozenSet();
#endif

    private static string? GetResourceFolderName(long type)
    {
        if (type == 12 || type is >= 6945 and <= 6953 || type is >= 393 and <= 405 ||
            ResourcePacksFilterIds.Contains(type))
            return "resourcepacks";

        if (type == 6 || type is >= 406 and <= 436 || ModFilterIds.Contains(type))
            return "mods";

        if (type is >= 6552 and <= 6555)
            return "shaderpacks";

        return null;
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

    public static async Task<CurseForgeAddonInfo[]> GetModProjectDetails(
        ICurseForgeApiService curseForgeApiService,
        ArraySegment<long> ids,
        bool useOfficialApi = false)
    {
        if (ids.Count == 0) return [];

        try
        {
            return await curseForgeApiService.GetAddons(ids, useOfficialApi) ?? [];
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode != HttpStatusCode.BadRequest &&
                e.StatusCode != HttpStatusCode.UnprocessableEntity)
                throw;

            return await RetryLogicAsync();
        }
        catch (CurseForgeAddonResolveException)
        {
            return await RetryLogicAsync();
        }

        async Task<CurseForgeAddonInfo[]> RetryLogicAsync()
        {
            if (ids.Count <= 1)
            {
                await Task.Delay(Random.Shared.Next(300, 600));
                return await curseForgeApiService.GetAddons(ids, true) ?? [];
            }

            await Task.Delay(2000);

            var mid = ids.Count / 2;
            var leftTask = GetModProjectDetails(curseForgeApiService, ids[..mid], true);
            var rightTask = GetModProjectDetails(curseForgeApiService, ids[mid..], true);
            var files = await Task.WhenAll(leftTask, rightTask);

            return
            [
                .. files[0],
                .. files[1]
            ];
        }
    }

    public static async Task<CurseForgeLatestFileModel[]> GetModPackFiles(
        ICurseForgeApiService curseForgeApiService,
        ArraySegment<long> ids,
        bool useOfficialApi = false)
    {
        if (ids.Count == 0) return [];

        try
        {
            return await curseForgeApiService.GetFiles(ids, useOfficialApi) ?? [];
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode != HttpStatusCode.BadRequest &&
                e.StatusCode != HttpStatusCode.UnprocessableEntity &&
                e.InnerException is not IOException)
                throw;

            return await RetryLogicAsync();
        }
        catch (CurseForgeFileResolveException)
        {
            return await RetryLogicAsync();
        }

        async Task<CurseForgeLatestFileModel[]> RetryLogicAsync()
        {
            if (ids.Count <= 1)
            {
                await Task.Delay(Random.Shared.Next(300, 600));
                return await curseForgeApiService.GetFiles(ids) ?? [];
            }

            await Task.Delay(2000);

            var mid = ids.Count / 2;
            var leftTask = GetModPackFiles(curseForgeApiService, ids[..mid], true);
            var rightTask = GetModPackFiles(curseForgeApiService, ids[mid..], true);
            var files = await Task.WhenAll(leftTask, rightTask);

            return
            [
                .. files[0],
                .. files[1]
            ];
        }
    }

    private static IEnumerable<string> GeneratePossibleDownloadUrls(long fileId, string fileName)
    {
        var fileIdStr = fileId.ToString();

        yield return $"https://edge.forgecdn.net/files/{fileIdStr[..4]}/{fileIdStr[4..]}/{fileName}";
        yield return $"https://mediafiles.forgecdn.net/files/{fileIdStr[..4]}/{fileIdStr[4..]}/{fileName}";
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
            var pendingCheckUrls = GeneratePossibleDownloadUrls(fileId, fileName);
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
}