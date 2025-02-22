﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
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

    public string? AssetIndexUriRoot { get; init; }

    public IReadOnlyList<string> AssetUriRoots { get; init; } = ["https://resources.download.minecraft.net/"];

    public IReadOnlyList<VersionManifestVersionsModel>? Versions { get; init; }

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame)
    {
        if (!checkLocalFiles) yield break;

        this.OnResolve("开始进行游戏资源(Asset)检查", ProgressValue.Start);

        if (resolvedGame.AssetInfo == null) yield break;

        var versions = this.Versions;
        if ((this.Versions?.Count ?? 0) == 0)
        {
            this.OnResolve("没有提供 Version Manifest， 开始下载", ProgressValue.Start);

            using var vmJsonRes = await HttpHelper.Get(DefaultVersionManifestUrl);
            var vm = await vmJsonRes.Content.ReadFromJsonAsync(VersionManifestContext.Default.VersionManifest);

            versions = vm?.Versions?.ToList();
        }

        if ((versions?.Count ?? 0) == 0) yield break;

        var isAssetInfoNotExists =
            string.IsNullOrEmpty(resolvedGame.AssetInfo?.Url) &&
            string.IsNullOrEmpty(resolvedGame.AssetInfo?.Id);
        if (isAssetInfoNotExists &&
            string.IsNullOrEmpty(resolvedGame.Assets))
            yield break;

        var assetIndexesDi =
            new DirectoryInfo(Path.Combine(basePath, GamePathHelper.GetAssetsRoot(), "indexes"));
        var assetObjectsDi =
            new DirectoryInfo(Path.Combine(basePath, GamePathHelper.GetAssetsRoot(), "objects"));

        if (!assetIndexesDi.Exists) assetIndexesDi.Create();
        if (!assetObjectsDi.Exists) assetObjectsDi.Create();

        var id = resolvedGame.AssetInfo?.Id ?? resolvedGame.Assets;
        var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{id}.json");
        if (!File.Exists(assetIndexesPath))
        {
            this.OnResolve("没有发现 Asset Indexes 文件， 开始下载", ProgressValue.Start);

            var assetIndexDownloadUri = resolvedGame.AssetInfo?.Url;

            if (isAssetInfoNotExists)
            {
                var versionObject =
                    versions?.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

                if (versionObject == null) yield break;

                using var jsonRes = await HttpHelper.Get(versionObject.Url);
                var versionModel =
                    await jsonRes.Content.ReadFromJsonAsync(RawVersionModelContext.Default.RawVersionModel);

                if (versionModel == null) yield break;

                assetIndexDownloadUri = versionModel.AssetIndex?.Url;
            }

            if (string.IsNullOrEmpty(assetIndexDownloadUri)) yield break;

            if (!string.IsNullOrEmpty(AssetIndexUriRoot))
            {
                var assetIndexUriRoot = HttpHelper.RegexMatchUri(assetIndexDownloadUri);
                assetIndexDownloadUri =
                    $"{this.AssetIndexUriRoot.TrimEnd('/')}{assetIndexDownloadUri[assetIndexUriRoot.Length..]}";
            }

            var dp = new SimpleDownloadFile
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
                await JsonSerializer.DeserializeAsync(assetFs, AssetObjectModelContext.Default.AssetObjectModel);
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

        foreach (var (key, fi) in assetObject.Objects)
        {
            var hash = fi.Hash;
            var twoDigitsHash = hash[..2];
            var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
            var filePath = Path.Combine(path, fi.Hash);

            var addedCheckedObject = Interlocked.Increment(ref checkedObject);
            var progress = ProgressValue.Create(addedCheckedObject, objectCount);

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
                Urls = AssetUriRoots.Select(r => $"{r}{twoDigitsHash}/{fi.Hash}").ToImmutableList(),
                FileSize = fi.Size,
                CheckSum = hash,
                FileName = hash
            };
        }

        this.OnResolve("Assets 解析完成", ProgressValue.Finished);
    }
}