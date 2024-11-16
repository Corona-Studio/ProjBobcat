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
        if (!this.CheckLocalFiles) yield break;
        if (this.VersionInfo.Logging?.Client?.File == null) yield break;
        if (string.IsNullOrEmpty(this.VersionInfo.Logging?.Client?.File.Url)) yield break;

        var fileName = Path.GetFileName(this.VersionInfo.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var loggingPath = GamePathHelper.GetLoggingPath(this.BasePath);
        var filePath = Path.Combine(loggingPath, fileName);

        if (File.Exists(filePath))
        {
            if (string.IsNullOrEmpty(this.VersionInfo.Logging?.Client?.File?.Sha1)) yield break;

            await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs));

            if (computedHash.Equals(this.VersionInfo.Logging?.Client?.File?.Sha1, StringComparison.OrdinalIgnoreCase))
                yield break;
        }

        if (string.IsNullOrEmpty(this.VersionInfo.Logging?.Client?.File?.Url))
            yield break;

        yield return new GameLoggingDownloadInfo
        {
            CheckSum = this.VersionInfo.Logging?.Client?.File?.Sha1,
            FileName = fileName,
            FileSize = this.VersionInfo.Logging?.Client?.File?.Size ?? 0,
            Path = loggingPath,
            Title = fileName,
            Type = ResourceType.Logging,
            Url = this.VersionInfo.Logging!.Client!.File!.Url!
        };
    }
}