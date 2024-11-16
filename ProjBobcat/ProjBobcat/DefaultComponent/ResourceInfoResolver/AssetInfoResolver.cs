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
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class AssetInfoResolver : ResolverBase
{
    const string DefaultVersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
    readonly string? _assetIndexUrlRoot;

    public string? AssetIndexUriRoot
    {
        get => this._assetIndexUrlRoot;
        init => this._assetIndexUrlRoot = value?.TrimEnd('/');
    }

    public string AssetUriRoot { get; init; } = "https://resources.download.minecraft.net/";

    public IReadOnlyList<VersionManifestVersionsModel>? Versions { get; init; }

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (!this.CheckLocalFiles) yield break;

        this.OnResolve("开始进行游戏资源(Asset)检查");

        if (this.VersionInfo?.AssetInfo == null) yield break;

        var versions = this.Versions;
        if ((this.Versions?.Count ?? 0) == 0)
        {
            this.OnResolve("没有提供 Version Manifest， 开始下载");

            using var vmJsonRes = await HttpHelper.Get(DefaultVersionManifestUrl);
            var vm = await vmJsonRes.Content.ReadFromJsonAsync(VersionManifestContext.Default.VersionManifest);

            versions = vm?.Versions?.ToList();
        }

        if ((versions?.Count ?? 0) == 0) yield break;

        var isAssetInfoNotExists =
            string.IsNullOrEmpty(this.VersionInfo.AssetInfo?.Url) &&
            string.IsNullOrEmpty(this.VersionInfo.AssetInfo?.Id);
        if (isAssetInfoNotExists &&
            string.IsNullOrEmpty(this.VersionInfo.Assets))
            yield break;

        var assetIndexesDi =
            new DirectoryInfo(Path.Combine(this.BasePath, GamePathHelper.GetAssetsRoot(), "indexes"));
        var assetObjectsDi =
            new DirectoryInfo(Path.Combine(this.BasePath, GamePathHelper.GetAssetsRoot(), "objects"));

        if (!assetIndexesDi.Exists) assetIndexesDi.Create();
        if (!assetObjectsDi.Exists) assetObjectsDi.Create();

        var id = this.VersionInfo.AssetInfo?.Id ?? this.VersionInfo.Assets;
        var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{id}.json");
        if (!File.Exists(assetIndexesPath))
        {
            this.OnResolve("没有发现 Asset Indexes 文件， 开始下载");

            var assetIndexDownloadUri = this.VersionInfo?.AssetInfo?.Url;

            if (isAssetInfoNotExists)
            {
                var versionObject =
                    versions?.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

                if (versionObject == default) yield break;

                using var jsonRes = await HttpHelper.Get(versionObject.Url);
                var versionModel =
                    await jsonRes.Content.ReadFromJsonAsync(RawVersionModelContext.Default.RawVersionModel);

                if (versionModel == default) yield break;

                assetIndexDownloadUri = versionModel.AssetIndex?.Url;
            }

            if (string.IsNullOrEmpty(assetIndexDownloadUri)) yield break;

            if (!string.IsNullOrEmpty(this.AssetIndexUriRoot))
            {
                var assetIndexUriRoot = HttpHelper.RegexMatchUri(assetIndexDownloadUri);
                assetIndexDownloadUri =
                    $"{this.AssetIndexUriRoot}{assetIndexDownloadUri[assetIndexUriRoot.Length..]}";
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
                this.OnResolve($"解析Asset Indexes 文件失败！原因：{e.Message}");
                yield break;
            }

            this.OnResolve("Asset Indexes 文件下载完成", 100);
        }

        this.OnResolve("开始解析Asset Indexes 文件...");

        AssetObjectModel? assetObject;
        try
        {
            await using var assetFs = File.OpenRead(assetIndexesPath);
            assetObject =
                await JsonSerializer.DeserializeAsync(assetFs, AssetObjectModelContext.Default.AssetObjectModel);
        }
        catch (Exception ex)
        {
            this.OnResolve($"解析Asset Indexes 文件失败！原因：{ex.Message}");

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
            this.OnResolve("解析Asset Indexes 文件失败！原因：文件可能损坏或为空");

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

        this.OnResolve("检索并验证 Asset 资源");

        foreach (var (key, fi) in assetObject.Objects)
        {
            var hash = fi.Hash;
            var twoDigitsHash = hash[..2];
            var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
            var filePath = Path.Combine(path, fi.Hash);

            Interlocked.Increment(ref checkedObject);
            var progress = (double)checkedObject / objectCount * 100;
            this.OnResolve(key.CropStr(20), progress);

            if (File.Exists(filePath))
            {
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs));

                if (computedHash.Equals(fi.Hash, StringComparison.OrdinalIgnoreCase)) continue;
            }

            yield return new AssetDownloadInfo
            {
                Title = hash,
                Path = path,
                Type = ResourceType.Asset,
                Url = $"{this.AssetUriRoot}{twoDigitsHash}/{fi.Hash}",
                FileSize = fi.Size,
                CheckSum = hash,
                FileName = hash
            };
        }

        this.OnResolve("Assets 解析完成", 100);
    }
}