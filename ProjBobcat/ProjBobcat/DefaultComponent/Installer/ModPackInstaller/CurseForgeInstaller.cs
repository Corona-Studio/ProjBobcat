using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        this.InvokeStatusChangedEvent("Starting installation", ProgressValue.Start);

        await this.DownloadModsTaskAsync();
        await this.InstallOverridesTaskAsync();

        this.InvokeStatusChangedEvent("Installation completed", ProgressValue.Finished);
    }

    public override async Task DownloadModsTaskAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(this.GameId);
        ArgumentException.ThrowIfNullOrEmpty(this.RootPath);
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = await ReadManifestTask(this.ModPackPath);

        ArgumentNullException.ThrowIfNull(manifest, "Failed to read the CurseForge manifest file.");

        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));
        var retryCount = Math.Max(1, this.RetryCount);

        this.NeedToDownload = manifest.Files?.Length ?? 0;

        var fileIds = manifest.Files
            ?.Where(file => file.ProjectId != 0 && file.FileId != 0)
            .Select(file => file.FileId)
            .ToArray() ?? [];

        CurseForgeLatestFileModel[]? files = null;

        for (var i = 0; i < retryCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        ArgumentNullException.ThrowIfNull(files, "Failed to retrieve the CurseForge file list.");

        var missingFileIds = fileIds.Except(files.Select(file => file.Id)).ToArray();

        // Fetch missing files if any
        for (var i = 0; i < retryCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        for (var i = 0; i < retryCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        ArgumentNullException.ThrowIfNull(modProjectDetails, "Failed to retrieve the CurseForge mod list.");
        ArgumentOutOfRangeException.ThrowIfLessThan(fileIds.Length, files.Length);

        var missingProjectIds = projectIds.Except(modProjectDetails.Select(mod => mod.Id)).ToArray();

        // Fetch missing projects if any
        for (var i = 0; i < retryCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                var proceededUrls = this.DownloadUriReplacer == null
                    ? GeneratePossibleDownloadUrls(file.Id, file.FileName)
                    :
                    [
                        .. this.DownloadUriReplacer(GeneratePossibleDownloadUrls(file.Id, file.FileName)),
                        ..fallbackUrls
                    ];

                var guessDownloadFile = new MultiSourceDownloadFile
                {
                    DownloadPath = di.FullName,
                    DownloadUris = [.. proceededUrls.Distinct().Select(u => new DownloadUriInfo(u, 1))],
                    FileName = file.FileName
                };

                downloadFiles.Add(guessDownloadFile);
                continue;
            }

            IEnumerable<string> urls = this.DownloadUriReplacer == null
                ? [downloadUrl]
                : [.. this.DownloadUriReplacer([downloadUrl]), downloadUrl];

            var fileDownloadPath = Path.Combine(di.FullName, file.FileName);
            var fileHash = file.Hashes?.FirstOrDefault(h => h.Algorithm == 1)?.Value;

            if (File.Exists(fileDownloadPath) && !string.IsNullOrEmpty(fileHash))
                try
                {
                    // Check local file
                    await using var fs = File.OpenRead(fileDownloadPath);
                    var computedSha1 = await SHA1.HashDataAsync(fs);
                    if (Convert.ToHexString(computedSha1).Equals(fileHash, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                catch (Exception)
                {
                    // ignored
                }


            var downloadFile = new MultiSourceDownloadFile
            {
                DownloadPath = di.FullName,
                DownloadUris = [.. urls.Distinct().Select(u => new DownloadUriInfo(u, 1))],
                FileName = file.FileName
            };

            downloadFiles.Add(downloadFile);
        }

        this.InvokeStatusChangedEvent("Successfully resolved the modpack mod download URLs", ProgressValue.Finished);

        await this.DownloadFilesTaskAsync(downloadFiles, new DownloadSettings
        {
            DownloadParts = 2,
            RetryCount = this.GetDownloadRetryCount(downloadFiles),
            Timeout = TimeSpan.FromMinutes(5),
            HttpClientFactory = this.HttpClientFactory
        }, cancellationToken);

        this.ThrowIfDownloadsFailed();
    }

    public override async Task InstallOverridesTaskAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(this.GameId);
        ArgumentException.ThrowIfNullOrEmpty(this.RootPath);
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = await ReadManifestTask(this.ModPackPath);

        ArgumentNullException.ThrowIfNull(manifest, "Failed to read the CurseForge manifest file.");

        var idPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(this.GameId));

        var modPackFullPath = Path.GetFullPath(this.ModPackPath);
        var gbk = Encoding.GetEncoding("GBK");

        await using var modPackFs = File.OpenRead(modPackFullPath);
        await using var archive = new ZipArchive(modPackFs, ZipArchiveMode.Read, true, gbk);

        this.TotalDownloaded = 0;
        this.NeedToDownload = archive.Entries.Count;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            this.InvokeStatusChangedEvent($"Extracting installation file: {subPathName}", progress);

            await using var fs = File.OpenWrite(path);
            await using var entryStream = await entry.OpenAsync();

            await entryStream.CopyToAsync(fs, cancellationToken);

            this.TotalDownloaded++;
        }
    }

    private static readonly FrozenSet<long> ResourcePacksFilterIds =
#if NET9_0_OR_GREATER
        FrozenSet.Create<long>(4465, 5193, 5244);
#else
        new[] { 4465L, 5193L, 5244L }.ToFrozenSet();
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
        new[]
        {
            4485L, 4545L, 4558L,
            4671L, 4672L, 4773L,
            4843L, 4906L, 5191L,
            5232L, 5299L, 5314L,
            6145L, 6484L, 6814L,
            6821L, 6954L
        }.ToFrozenSet();
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
        await using var archive = new ZipArchive(fullPackFs, ZipArchiveMode.Read);

        var manifestEntry =
            archive.Entries.FirstOrDefault(x =>
                x.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry == null)
            return null;

        await using var stream = await manifestEntry.OpenAsync();

        var manifestModel =
            await JsonSerializer.DeserializeAsync(stream, SerializerContext.Default.CurseForgeManifestModel);

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
