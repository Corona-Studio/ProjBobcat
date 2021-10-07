using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class AssetInfoResolver : IResourceInfoResolver
    {
        readonly string _assetIndexUrlRoot;

        string _basePath;

        public string AssetIndexUriRoot
        {
            get => _assetIndexUrlRoot;
            init => _assetIndexUrlRoot = value.TrimEnd('/');
        }

        public string AssetUriRoot { get; init; } = "https://resources.download.minecraft.net/";

        public bool CheckLocalFiles { get; set; }

        public string BasePath
        {
            get => _basePath.TrimEnd('\\');
            set => _basePath = value;
        }

        public VersionInfo VersionInfo { get; set; }
        public List<VersionManifestVersionsModel> Versions { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;

        public IEnumerable<IGameResource> ResolveResource()
        {
            var itr = ResolveResourceAsync().GetAsyncEnumerator();
            while (itr.MoveNextAsync().Result) yield return itr.Current;
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
        {
            LogGameResourceInfoResolveStatus("开始进行游戏资源(Asset)检查");

            if (!(Versions?.Any() ?? false) && VersionInfo?.AssetInfo == null) yield break;

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
                LogGameResourceInfoResolveStatus("没有发现Asset Indexes 文件， 开始下载");

                var assetIndexDownloadUri = VersionInfo?.AssetInfo?.Url;

                if (isAssetInfoNotExists)
                {
                    var versionObject = Versions?.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (versionObject == default) yield break;

                    var jsonRes = await HttpHelper.Get(versionObject.Url);
                    var jsonStr = await jsonRes.Content.ReadAsStringAsync();
                    var versionModel = JsonConvert.DeserializeObject<RawVersionModel>(jsonStr);

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
                    LogGameResourceInfoResolveStatus($"解析Asset Indexes 文件失败！原因：{e.Message}", logType: LogType.Error);
                    yield break;
                }

                LogGameResourceInfoResolveStatus("Asset Indexes 文件下载完成", 100, LogType.Success);
            }

            LogGameResourceInfoResolveStatus("开始解析Asset Indexes 文件...");

            AssetObjectModel assetObject;
            try
            {
                var content = await File.ReadAllTextAsync(assetIndexesPath);
                assetObject = JsonConvert.DeserializeObject<AssetObjectModel>(content);
            }
            catch (Exception ex)
            {
                LogGameResourceInfoResolveStatus($"解析Asset Indexes 文件失败！原因：{ex.Message}", logType: LogType.Error);
                File.Delete(assetIndexesPath);
                yield break;
            }

            if (assetObject == null)
            {
                LogGameResourceInfoResolveStatus("解析Asset Indexes 文件失败！原因：文件可能损坏或为空", logType: LogType.Error);
                File.Delete(assetIndexesPath);
                yield break;
            }

#pragma warning disable CA5350 // 不要使用弱加密算法
            using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

            var checkedObject = 0;
            var objectCount = assetObject.Objects.Count;
            foreach (var (_, fi) in assetObject.Objects)
            {
                var hash = fi.Hash;
                var twoDigitsHash = hash[..2];
                var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);
                var filePath = Path.Combine(path, fi.Hash);

                checkedObject++;
                var progress = checkedObject / objectCount * 100;
                LogGameResourceInfoResolveStatus($"检索并验证 Asset 资源：{hash.AsSpan().Slice(0, 10).ToString()}", progress);

                if (File.Exists(filePath))
                {
                    if (!CheckLocalFiles) continue;
                    try
                    {
                        var computedHash = await CryptoHelper.ComputeFileHashAsync(filePath, hA);
                        if (computedHash.Equals(fi.Hash, StringComparison.OrdinalIgnoreCase)) continue;

                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                yield return new AssetDownloadInfo
                {
                    Title = hash,
                    Path = path,
                    Type = "Asset",
                    Uri = $"{AssetUriRoot}{twoDigitsHash}/{fi.Hash}",
                    FileSize = fi.Size,
                    CheckSum = hash,
                    FileName = hash
                };
            }

            LogGameResourceInfoResolveStatus("Assets 解析完成", 100, logType: LogType.Success);
        }

        void LogGameResourceInfoResolveStatus(string currentStatus, double progress = 0, LogType logType = LogType.Normal)
        {
            GameResourceInfoResolveEvent?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                Status = currentStatus,
                Progress = progress,
                LogType = logType
            });
        }
    }
}