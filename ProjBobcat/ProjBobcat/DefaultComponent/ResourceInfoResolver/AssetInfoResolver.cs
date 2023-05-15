using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class AssetInfoResolver : ResolverBase
{
    const string DefaultVersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
    readonly string _assetIndexUrlRoot;

    public string AssetIndexUriRoot
    {
        get => _assetIndexUrlRoot;
        init => _assetIndexUrlRoot = value.TrimEnd('/');
    }

    public string AssetUriRoot { get; init; } = "https://resources.download.minecraft.net/";

    public IEnumerable<VersionManifestVersionsModel>? Versions { get; init; }

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) yield break;

        OnResolve("开始进行游戏资源(Asset)检查");

        if (VersionInfo?.AssetInfo == null) yield break;

        var versions = Versions;
        if (!(Versions?.Any() ?? false))
        {
            OnResolve("没有提供 Version Manifest， 开始下载");

            using var vmJsonRes = await HttpHelper.Get(DefaultVersionManifestUrl);
            var vm = await vmJsonRes.Content.ReadFromJsonAsync(VersionManifestContext.Default.VersionManifest);

            versions = vm?.Versions?.ToList();
        }

        if (!(versions?.Any() ?? false)) yield break;

        var isAssetInfoNotExists =
            string.IsNullOrEmpty(VersionInfo?.AssetInfo?.Url) &&
            string.IsNullOrEmpty(VersionInfo?.AssetInfo?.Id);
        if (isAssetInfoNotExists &&
            string.IsNullOrEmpty(VersionInfo?.Assets))
            yield break;

        var assetIndexesDi =
            new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetAssetsRoot(), "indexes"));
        var assetObjectsDi =
            new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetAssetsRoot(), "objects"));

        if (!assetIndexesDi.Exists) assetIndexesDi.Create();
        if (!assetObjectsDi.Exists) assetObjectsDi.Create();

        var id = VersionInfo?.AssetInfo?.Id ?? VersionInfo.Assets;
        var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{id}.json");
        if (!File.Exists(assetIndexesPath))
        {
            OnResolve("没有发现 Asset Indexes 文件， 开始下载");

            var assetIndexDownloadUri = VersionInfo?.AssetInfo?.Url;

            if (isAssetInfoNotExists)
            {
                var versionObject = versions.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (versionObject == default) yield break;

                using var jsonRes = await HttpHelper.Get(versionObject.Url);
                var versionModel =
                    await jsonRes.Content.ReadFromJsonAsync(RawVersionModelContext.Default.RawVersionModel);

                if (versionModel == default) yield break;

                assetIndexDownloadUri = versionModel.AssetIndex?.Url;
            }

            if (string.IsNullOrEmpty(assetIndexDownloadUri)) yield break;

            if (!string.IsNullOrEmpty(AssetIndexUriRoot))
            {
                var assetIndexUriRoot = HttpHelper.RegexMatchUri(assetIndexDownloadUri);
                assetIndexDownloadUri =
                    $"{AssetIndexUriRoot}{assetIndexDownloadUri[assetIndexUriRoot.Length..]}";
            }

            var dp = new DownloadFile
            {
                DownloadPath = assetIndexesDi.FullName,
                FileName = $"{id}.json",
                DownloadUri = assetIndexDownloadUri
            };

            try
            {
                await DownloadHelper.DownloadData(dp);
            }
            catch (Exception e)
            {
                OnResolve($"解析Asset Indexes 文件失败！原因：{e.Message}");
                yield break;
            }

            OnResolve("Asset Indexes 文件下载完成", 100);
        }

        OnResolve("开始解析Asset Indexes 文件...");

        AssetObjectModel? assetObject;
        try
        {
            await using var assetFs = File.OpenRead(assetIndexesPath);
            assetObject =
                await JsonSerializer.DeserializeAsync(assetFs, AssetObjectModelContext.Default.AssetObjectModel);
        }
        catch (Exception ex)
        {
            OnResolve($"解析Asset Indexes 文件失败！原因：{ex.Message}");
            File.Delete(assetIndexesPath);
            yield break;
        }

        if (assetObject == null)
        {
            OnResolve("解析Asset Indexes 文件失败！原因：文件可能损坏或为空");
            File.Delete(assetIndexesPath);
            yield break;
        }

        var checkedObject = 0;
        var objectCount = assetObject.Objects.Count;

        OnResolve("检索并验证 Asset 资源");

        foreach (var obj in assetObject.Objects)
        {
            var (_, fi) = obj;
            var hash = fi.Hash;
            var twoDigitsHash = hash[..2];
            var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
            var filePath = Path.Combine(path, fi.Hash);

            Interlocked.Increment(ref checkedObject);
            var progress = (double)checkedObject / objectCount * 100;
            OnResolve(string.Empty, progress);

            if (File.Exists(filePath))
            {
#if NET7_0_OR_GREATER
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = (await SHA1.HashDataAsync(fs)).BytesToString();
#else
                var bytes = await File.ReadAllBytesAsync(filePath);
                var computedHash = SHA1.HashData(bytes.AsSpan()).BytesToString();
#endif
                
                if (computedHash.Equals(fi.Hash, StringComparison.OrdinalIgnoreCase)) continue;
            }

            yield return new AssetDownloadInfo
            {
                Title = hash,
                Path = path,
                Type = ResourceType.Asset,
                Url = $"{AssetUriRoot}{twoDigitsHash}/{fi.Hash}",
                FileSize = fi.Size,
                CheckSum = hash,
                FileName = hash
            };
        }

        OnResolve("Assets 解析完成", 100);
    }
}