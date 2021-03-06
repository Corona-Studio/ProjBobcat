﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class AssetInfoResolver : IResourceInfoResolver
    {
        private string _assetIndexUrlRoot;

        private string _basePath;

        public string AssetIndexUriRoot
        {
            get => _assetIndexUrlRoot;
            set => _assetIndexUrlRoot = value.TrimEnd('/');
        }

        public string AssetUriRoot { get; set; } = "https://resources.download.minecraft.net/";

        public string BasePath
        {
            get => _basePath.TrimEnd('\\');
            set => _basePath = value;
        }

        public VersionInfo VersionInfo { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;

        public IEnumerable<IGameResource> ResolveResource()
        {
            var itr = ResolveResourceAsync().GetAsyncEnumerator();
            while (itr.MoveNextAsync().Result) yield return itr.Current;
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
        {
            LogGameResourceInfoResolveStatus("开始进行游戏资源(Asset)检查");
            if (string.IsNullOrEmpty(VersionInfo?.AssetInfo.Url)) yield break;
            if (string.IsNullOrEmpty(VersionInfo?.AssetInfo.Id)) yield break;

            var assetIndexesDi =
                new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetAssetsRoot(), "indexes"));
            var assetObjectsDi =
                new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetAssetsRoot(), "objects"));

            if (!assetIndexesDi.Exists) assetIndexesDi.Create();
            if (!assetObjectsDi.Exists) assetObjectsDi.Create();

            var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{VersionInfo.AssetInfo.Id}.json");
            if (!File.Exists(assetIndexesPath))
            {
                LogGameResourceInfoResolveStatus("没有发现Asset Indexes 文件， 开始下载");
                var assetIndexDownloadUri = VersionInfo.AssetInfo.Url;
                if (!string.IsNullOrEmpty(AssetIndexUriRoot))
                {
                    var assetIndexUriRoot = HttpHelper.RegexMatchUri(VersionInfo.AssetInfo.Url).TrimEnd('/');
                    assetIndexDownloadUri =
                        $"{AssetIndexUriRoot}{assetIndexDownloadUri[assetIndexUriRoot.Length..]}";
                }

                var dp = new DownloadFile
                {
                    DownloadPath = assetIndexesDi.FullName,
                    FileName = $"{VersionInfo.AssetInfo.Id}.json",
                    DownloadUri = assetIndexDownloadUri
                };

                try
                {
                    await DownloadHelper.DownloadData(dp);
                }
                catch (Exception e)
                {
                    LogGameResourceInfoResolveStatus($"解析Asset Indexes 文件失败！原因：{e.Message}", LogType.Error);
                    yield break;
                }

                LogGameResourceInfoResolveStatus("Asset Indexes 文件下载完成", LogType.Success);
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
                LogGameResourceInfoResolveStatus($"解析Asset Indexes 文件失败！原因：{ex.Message}", LogType.Error);
                File.Delete(assetIndexesPath);
                yield break;
            }

            if (assetObject == null)
            {
                LogGameResourceInfoResolveStatus("解析Asset Indexes 文件失败！原因：文件可能损坏或为空", LogType.Error);
                File.Delete(assetIndexesPath);
                yield break;
            }

            foreach (var asset in assetObject.Objects)
            {
                var hash = asset.Value.Hash;
                var twoDigitsHash = hash[..2];
                var path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash);

                if (File.Exists(Path.Combine(path, asset.Value.Hash))) continue;

                yield return new AssetDownloadInfo
                {
                    Title = hash,
                    Path = path,
                    Type = "Asset",
                    Uri = $"{AssetUriRoot}{twoDigitsHash}/{asset.Value.Hash}",
                    FileSize = asset.Value.Size,
                    CheckSum = hash,
                    FileName = hash
                };
            }

            LogGameResourceInfoResolveStatus("Assets 解析完成", LogType.Success);
        }

        private void LogGameResourceInfoResolveStatus(string currentStatus, LogType logType = LogType.Normal)
        {
            GameResourceInfoResolveEvent?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                CurrentProgress = currentStatus,
                LogType = logType
            });
        }
    }
}