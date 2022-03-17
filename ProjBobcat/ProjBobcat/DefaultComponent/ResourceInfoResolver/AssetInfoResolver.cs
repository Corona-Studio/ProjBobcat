using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public class AssetInfoResolver : ResolverBase
{
    readonly string _assetIndexUrlRoot;

    public string AssetIndexUriRoot
    {
        get => _assetIndexUrlRoot;
        init => _assetIndexUrlRoot = value.TrimEnd('/');
    }

    public string AssetUriRoot { get; init; } = "https://resources.download.minecraft.net/";

    public List<VersionManifestVersionsModel> Versions { get; set; }

    public override async Task<IEnumerable<IGameResource>> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) return Enumerable.Empty<IGameResource>();

        OnResolve("开始进行游戏资源(Asset)检查");

        if (!(Versions?.Any() ?? false) && VersionInfo?.AssetInfo == null) return Enumerable.Empty<IGameResource>();

        var isAssetInfoNotExists =
            string.IsNullOrEmpty(VersionInfo?.AssetInfo?.Url) &&
            string.IsNullOrEmpty(VersionInfo?.AssetInfo?.Id);
        if (isAssetInfoNotExists &&
            string.IsNullOrEmpty(VersionInfo?.Assets))
            return Enumerable.Empty<IGameResource>();

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
            OnResolve("没有发现Asset Indexes 文件， 开始下载");

            var assetIndexDownloadUri = VersionInfo?.AssetInfo?.Url;

            if (isAssetInfoNotExists)
            {
                var versionObject = Versions?.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (versionObject == default) return Enumerable.Empty<IGameResource>();

                var jsonRes = await HttpHelper.Get(versionObject.Url);
                var jsonStr = await jsonRes.Content.ReadAsStringAsync();
                var versionModel = JsonConvert.DeserializeObject<RawVersionModel>(jsonStr);

                if (versionModel == default) return Enumerable.Empty<IGameResource>();

                assetIndexDownloadUri = versionModel.AssetIndex?.Url;
            }

            if (string.IsNullOrEmpty(assetIndexDownloadUri)) return Enumerable.Empty<IGameResource>();

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
                await DownloadHelper.DownloadData(dp, DownloadSettings.Default);
            }
            catch (Exception e)
            {
                OnResolve($"解析Asset Indexes 文件失败！原因：{e.Message}");
                return Enumerable.Empty<IGameResource>();
            }

            OnResolve("Asset Indexes 文件下载完成", 100);
        }

        OnResolve("开始解析Asset Indexes 文件...");

        AssetObjectModel assetObject;
        try
        {
            var content = await File.ReadAllTextAsync(assetIndexesPath);
            assetObject = JsonConvert.DeserializeObject<AssetObjectModel>(content);
        }
        catch (Exception ex)
        {
            OnResolve($"解析Asset Indexes 文件失败！原因：{ex.Message}");
            File.Delete(assetIndexesPath);
            return Enumerable.Empty<IGameResource>();
        }

        if (assetObject == null)
        {
            OnResolve("解析Asset Indexes 文件失败！原因：文件可能损坏或为空");
            File.Delete(assetIndexesPath);
            return Enumerable.Empty<IGameResource>();
        }

        var checkedObject = 0;
        var objectCount = assetObject.Objects.Count;
        var result = new ConcurrentBag<IGameResource>();

        OnResolve("检索并验证 Asset 资源");

        var filesBlock =
            new TransformManyBlock<Dictionary<string, AssetFileInfo>, KeyValuePair<string, AssetFileInfo>>(
                chunk => chunk,
                new ExecutionDataflowBlockOptions());

        var resolveActionBlock = new ActionBlock<KeyValuePair<string, AssetFileInfo>>(async obj =>
        {
            var (_, fi) = obj;
            var hash = fi.Hash;
            var twoDigitsHash = hash[..2];
            var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
            var filePath = Path.Combine(path, fi.Hash);

            Interlocked.Increment(ref checkedObject);
            var progress = (double) checkedObject / objectCount * 100;
            OnResolve(string.Empty, progress);

            if (File.Exists(filePath))
            {
                var computedHash = CryptoHelper.ToString(SHA1.HashData(await File.ReadAllBytesAsync(filePath)));
                if (computedHash.Equals(fi.Hash, StringComparison.OrdinalIgnoreCase)) return;
            }

            result.Add(new AssetDownloadInfo
            {
                Title = hash,
                Path = path,
                Type = ResourceType.Asset,
                Uri = $"{AssetUriRoot}{twoDigitsHash}/{fi.Hash}",
                FileSize = fi.Size,
                CheckSum = hash,
                FileName = hash
            });
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            BoundedCapacity = MaxDegreeOfParallelism
        });

        var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

        filesBlock.LinkTo(resolveActionBlock, linkOptions);

        filesBlock.Post(assetObject.Objects);
        filesBlock.Complete();

        await resolveActionBlock.Completion;
        resolveActionBlock.Complete();

        OnResolve("Assets 解析完成", 100);

        return result;
    }
}