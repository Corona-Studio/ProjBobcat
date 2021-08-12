using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class VersionInfoResolver : IResourceInfoResolver
    {
        public string BasePath { get; set; }
        public bool CheckLocalFiles { get; set; }
        public VersionInfo VersionInfo { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;

        public IEnumerable<IGameResource> ResolveResource()
        {
            var itr = ResolveResourceAsync().GetAsyncEnumerator();
            while (itr.MoveNextAsync().Result) yield return itr.Current;
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
        {
            if (!CheckLocalFiles) yield break;

            var id = VersionInfo.RootVersion ?? VersionInfo.DirName;
            var versionJson = GamePathHelper.GetGameJsonPath(BasePath, id);

            if (!File.Exists(versionJson)) yield break;

            var fileContent = await File.ReadAllTextAsync(versionJson);
            var rawVersionModel = JsonConvert.DeserializeObject<RawVersionModel>(fileContent);

            if (rawVersionModel?.Downloads?.Client == null) yield break;

            var clientDownload = rawVersionModel.Downloads.Client;
            var jarPath = GamePathHelper.GetVersionJar(BasePath, id);

            var downloadInfo = new VersionJarDownloadInfo
            {
                CheckSum = clientDownload.Sha1,
                FileName = $"{id}.jar",
                FileSize = clientDownload.Size,
                Path = Path.Combine(BasePath, GamePathHelper.GetGamePath(id)),
                Title = $"{id}.jar",
                Type = "GameJar",
                Uri = clientDownload.Url
            };

            if (!File.Exists(jarPath))
            {
                yield return downloadInfo;
            }
            else
            {
                if (string.IsNullOrEmpty(clientDownload.Sha1)) yield break;

                using var hash = SHA1.Create();
                var computedHash = await CryptoHelper.ComputeFileHashAsync(jarPath, hash);

                if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase)) yield break;

                try
                {
                    File.Delete(jarPath);
                }
                catch (Exception)
                {
                }

                yield return downloadInfo;
            }
        }
    }
}