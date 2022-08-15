using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public class GameLoggingInfoResolver : ResolverBase
{
    public override async Task<IEnumerable<IGameResource>> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) return Enumerable.Empty<IGameResource>();
        if(VersionInfo.Logging?.Client == null) return Enumerable.Empty<IGameResource>();
        if (VersionInfo.Logging?.Client?.File == null) return Enumerable.Empty<IGameResource>();
        if (string.IsNullOrEmpty(VersionInfo.Logging?.Client?.File.Url)) return Enumerable.Empty<IGameResource>();

        var fileName = Path.GetFileName(VersionInfo.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) return Enumerable.Empty<IGameResource>();

        var loggingPath = GamePathHelper.GetLoggingPath(BasePath);
        var filePath = Path.Combine(loggingPath, fileName);

        if (File.Exists(filePath))
        {
            if (string.IsNullOrEmpty(VersionInfo.Logging?.Client?.File?.Sha1)) return Enumerable.Empty<IGameResource>();

            using var hash = SHA1.Create();
            var computedHash = await CryptoHelper.ComputeFileHashAsync(filePath, hash);

            if (computedHash.Equals(VersionInfo.Logging?.Client?.File?.Sha1, StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<IGameResource>();
        }

        var downloadInfo = new GameLoggingDownloadInfo
        {
            CheckSum = VersionInfo.Logging?.Client?.File?.Sha1,
            FileName = fileName,
            FileSize = VersionInfo.Logging?.Client?.File?.Size ?? 0,
            Path = loggingPath,
            Title = fileName,
            Type = ResourceType.Logging,
            Uri = VersionInfo.Logging?.Client?.File ?.Url
        };

        return new[] { downloadInfo };
    }
}