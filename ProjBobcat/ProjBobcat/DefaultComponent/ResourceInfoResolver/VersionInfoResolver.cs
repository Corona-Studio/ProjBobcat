using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public class VersionInfoResolver : ResolverBase
{
    public override async Task<IEnumerable<IGameResource>> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) return Enumerable.Empty<IGameResource>();

        var id = VersionInfo.RootVersion ?? VersionInfo.DirName;
        var versionJson = GamePathHelper.GetGameJsonPath(BasePath, id);

        if (!File.Exists(versionJson)) return Enumerable.Empty<IGameResource>();

        var fileContent = await File.ReadAllTextAsync(versionJson);
        var rawVersionModel = JsonConvert.DeserializeObject<RawVersionModel>(fileContent);

        if (rawVersionModel?.Downloads?.Client == null) return Enumerable.Empty<IGameResource>();

        var clientDownload = rawVersionModel.Downloads.Client;
        var jarPath = GamePathHelper.GetVersionJar(BasePath, id);
        

        if (File.Exists(jarPath))
        {
            if (string.IsNullOrEmpty(clientDownload.Sha1)) return Enumerable.Empty<IGameResource>();

            using var hash = SHA1.Create();
            var computedHash = await CryptoHelper.ComputeFileHashAsync(jarPath, hash);

            if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<IGameResource>();
        }
        
        var downloadInfo = new VersionJarDownloadInfo
        {
            CheckSum = clientDownload.Sha1,
            FileName = $"{id}.jar",
            FileSize = clientDownload.Size,
            Path = Path.Combine(BasePath, GamePathHelper.GetGamePath(id)),
            Title = $"{id}.jar",
            Type = ResourceType.GameJar,
            Uri = clientDownload.Url
        };

        return new[] { downloadInfo };
    }
}