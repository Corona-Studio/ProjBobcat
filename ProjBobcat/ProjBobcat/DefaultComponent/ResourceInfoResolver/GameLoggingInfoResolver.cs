using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class GameLoggingInfoResolver : ResolverBase
{
    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) yield break;
        if (VersionInfo.Logging?.Client?.File == null) yield break;
        if (string.IsNullOrEmpty(VersionInfo.Logging?.Client?.File.Url)) yield break;

        var fileName = Path.GetFileName(VersionInfo.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var loggingPath = GamePathHelper.GetLoggingPath(BasePath);
        var filePath = Path.Combine(loggingPath, fileName);

        if (File.Exists(filePath))
        {
            if (string.IsNullOrEmpty(VersionInfo.Logging?.Client?.File?.Sha1)) yield break;

#if NET7_0_OR_GREATER
            await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var computedHash = (await SHA1.HashDataAsync(fs)).BytesToString();
#else
            var bytes = await File.ReadAllBytesAsync(filePath);
            var computedHash = SHA1.HashData(bytes.AsSpan()).BytesToString();
#endif

            if (computedHash.Equals(VersionInfo.Logging?.Client?.File?.Sha1, StringComparison.OrdinalIgnoreCase))
                yield break;
        }

        yield return new GameLoggingDownloadInfo
        {
            CheckSum = VersionInfo.Logging?.Client?.File?.Sha1,
            FileName = fileName,
            FileSize = VersionInfo.Logging?.Client?.File?.Size ?? 0,
            Path = loggingPath,
            Title = fileName,
            Type = ResourceType.Logging,
            Url = VersionInfo.Logging?.Client?.File?.Url
        };
    }
}