using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class AssetInfoResolver : ResolverBase
{
    const string DefaultVersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";

    public string? VersionManifestUrl { get; init; }
    public IReadOnlyList<DownloadUriInfo>? AssetIndexUriRoots { get; init; }

    public IReadOnlyList<DownloadUriInfo> AssetUriRoots { get; init; } =
        [new("https://resources.download.minecraft.net/", 1)];

    public IReadOnlyList<VersionManifestVersionsModel>? Versions { get; init; }

    public required IHttpClientFactory HttpClientFactory { get; init; }

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!checkLocalFiles) yield break;
        
        cancellationToken.ThrowIfCancellationRequested();

        this.OnResolve("开始进行游戏资源(Asset)检查", ProgressValue.Start);

        if (resolvedGame.AssetInfo == null) yield break;

        var client = this.HttpClientFactory.CreateClient();
        var versions = this.Versions;

        var isAssetInfoNotExists =
            string.IsNullOrEmpty(resolvedGame.AssetInfo?.Url) &&
            string.IsNullOrEmpty(resolvedGame.AssetInfo?.Id);

        var id = resolvedGame.AssetInfo?.Id ?? resolvedGame.Assets;
        var assetIndexesDi =
            new DirectoryInfo(Path.Combine(basePath, GamePathHelper.GetAssetsRoot(), "indexes"));
        var assetObjectsDi =
            new DirectoryInfo(Path.Combine(basePath, GamePathHelper.GetAssetsRoot(), "objects"));

        if (!assetIndexesDi.Exists) assetIndexesDi.Create();
        if (!assetObjectsDi.Exists) assetObjectsDi.Create();

        var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{id}.json");
        var isAssetsIndexExists = File.Exists(assetIndexesPath);

        if ((this.Versions?.Count ?? 0) == 0 && !isAssetsIndexExists)
        {
            this.OnResolve("没有提供 Version Manifest， 开始下载", ProgressValue.Start);

            using var vmJsonReq =
                new HttpRequestMessage(HttpMethod.Get, VersionManifestUrl ?? DefaultVersionManifestUrl);
            using var vmJsonRes = await client.SendAsync(vmJsonReq, cancellationToken).ConfigureAwait(false);

            var vm = await vmJsonRes.Content.ReadFromJsonAsync(SerializerContext.Default.VersionManifest, cancellationToken).ConfigureAwait(false);

            versions = vm?.Versions?.ToList();

            if ((versions?.Count ?? 0) == 0) yield break;
        }

        if (isAssetInfoNotExists &&
            string.IsNullOrEmpty(resolvedGame.Assets))
            yield break;

        if (!isAssetsIndexExists)
        {
            this.OnResolve("没有发现 Asset Indexes 文件， 开始下载", ProgressValue.Start);

            var assetIndexDownloadUri = resolvedGame.AssetInfo?.Url;

            if (isAssetInfoNotExists)
            {
                var versionObject =
                    versions?.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

                if (versionObject == null) yield break;

                var fallbackUrls = new List<DownloadUriInfo>(AssetIndexUriRoots?.Count ?? 1);
                var initUrl = new DownloadUriInfo(versionObject.Url, 1);
                
                if (AssetIndexUriRoots is { Count: > 0 })
                {
                    foreach (var uriRoot in AssetIndexUriRoots)
                    {
                        var replacedUrl = initUrl with
                        {
                            DownloadUri = initUrl.DownloadUri
                                .Replace("https://piston-meta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                                .Replace("https://launchermeta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                                .Replace("https://launcher.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                        };

                        fallbackUrls.Add(replacedUrl);
                    }
                }
                else
                {
                    fallbackUrls.Add(initUrl);
                }

                foreach (var url in fallbackUrls)
                {
                    try
                    {
                        using var jsonRes = await client.GetAsync(url.DownloadUri, cancellationToken).ConfigureAwait(false);
                        var versionModel =
                            await jsonRes.Content.ReadFromJsonAsync(SerializerContext.Default.RawVersionModel, cancellationToken).ConfigureAwait(false);

                        if (versionModel == null) yield break;

                        assetIndexDownloadUri = versionModel.AssetIndex?.Url;
                        break;
                    }
                    catch (HttpRequestException)
                    {
                        // Try next fallback URL
                    }
                }
            }

            if (string.IsNullOrEmpty(assetIndexDownloadUri)) yield break;

            var urls = new List<DownloadUriInfo>(AssetIndexUriRoots?.Count ?? 1);
            
            if (AssetIndexUriRoots is { Count: > 0 })
            {
                var initUrl = new DownloadUriInfo(assetIndexDownloadUri, 1);

                foreach (var uriRoot in AssetIndexUriRoots)
                {
                    var replacedUrl = initUrl with
                    {
                        DownloadUri = initUrl.DownloadUri
                            .Replace("https://piston-meta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                            .Replace("https://launchermeta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                            .Replace("https://launcher.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                    };

                    urls.Add(replacedUrl);
                }
            }
            else
            {
                urls.Add(new(assetIndexDownloadUri, 1));
            }

            var dp = new MultiSourceDownloadFile
            {
                DownloadPath = assetIndexesDi.FullName,
                FileName = $"{id}.json",
                DownloadUris = urls
            };

            try
            {
                await DownloadHelper.DownloadData(dp, new DownloadSettings
                {
                    RetryCount = 6,
                    CheckFile = false,
                    Timeout = TimeSpan.FromMinutes(1),
                    DownloadParts = 1,
                    HttpClientFactory = HttpClientFactory
                });
            }
            catch (Exception e)
            {
                this.OnResolve($"解析Asset Indexes 文件失败！原因：{e.Message}", ProgressValue.Start);
                yield break;
            }

            this.OnResolve("Asset Indexes 文件下载完成", ProgressValue.Finished);
        }

        this.OnResolve("开始解析Asset Indexes 文件...", ProgressValue.Start);

        AssetObjectModel? assetObject;
        try
        {
            await using var assetFs = File.OpenRead(assetIndexesPath);
            assetObject =
                await JsonSerializer.DeserializeAsync(assetFs, SerializerContext.Default.AssetObjectModel);
        }
        catch (Exception ex)
        {
            this.OnResolve($"解析Asset Indexes 文件失败！原因：{ex.Message}", ProgressValue.Start);

            try
            {
                File.Delete(assetIndexesPath);
            }
            catch (IOException)
            {
            }

            yield break;
        }

        if (assetObject == null)
        {
            this.OnResolve("解析Asset Indexes 文件失败！原因：文件可能损坏或为空", ProgressValue.Start);

            try
            {
                File.Delete(assetIndexesPath);
            }
            catch (IOException)
            {
            }

            yield break;
        }

        var checkedObject = 0;
        var objectCount = assetObject.Objects.Count;

        this.OnResolve("检索并验证 Asset 资源", ProgressValue.Start);

        var channel = System.Threading.Channels.Channel.CreateUnbounded<IGameResource>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount * 2, 16),
            CancellationToken = cancellationToken
        };

        var processingTask = Parallel.ForEachAsync(assetObject.Objects, parallelOptions, async (kvp, ct) =>
        {
            var (key, fi) = kvp;
            var hash = fi.Hash;
            var twoDigitsHash = hash[..2];
            var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
            var filePath = Path.Combine(path, fi.Hash);

            var addedCheckedObject = Interlocked.Increment(ref checkedObject);
            
            // Report progress every 100 files to reduce overhead
            if (addedCheckedObject % 100 == 0 || addedCheckedObject == objectCount)
            {
                var progress = ProgressValue.Create(addedCheckedObject, objectCount);
                this.OnResolve(key.CropStr(20), progress);
            }

            var needsDownload = !File.Exists(filePath);
            
            if (!needsDownload && !string.IsNullOrEmpty(fi.Hash))
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                
                try
                {
                    await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs, cts.Token).ConfigureAwait(false));
                    needsDownload = !computedHash.Equals(fi.Hash, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    needsDownload = true;
                }
            }

            if (needsDownload)
            {
                var downloadInfo = new AssetDownloadInfo
                {
                    Title = hash,
                    Path = path,
                    Type = ResourceType.Asset,
                    Urls = [.. this.AssetUriRoots.Select(r => r with { DownloadUri = $"{r.DownloadUri}{twoDigitsHash}/{fi.Hash}" })],
                    FileSize = fi.Size,
                    CheckSum = hash,
                    FileName = hash
                };
                await channel.Writer.WriteAsync(downloadInfo, ct).ConfigureAwait(false);
            }
        });

        // Complete channel when processing is done
        _ = processingTask.ContinueWith(_ => channel.Writer.Complete(), TaskScheduler.Default);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        await processingTask.ConfigureAwait(false);

        this.OnResolve("Assets 解析完成", ProgressValue.Finished);
    }
}