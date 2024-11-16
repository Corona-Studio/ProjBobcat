using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class VersionInfoResolver : ResolverBase
{
    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (!this.CheckLocalFiles) yield break;

        var id = this.VersionInfo.RootVersion ?? this.VersionInfo.DirName;
        var versionJson = GamePathHelper.GetGameJsonPath(this.BasePath, id);

        if (!File.Exists(versionJson)) yield break;

        await using var fs = File.OpenRead(versionJson);
        var rawVersionModel = await JsonSerializer.DeserializeAsync(fs, RawVersionModelContext.Default.RawVersionModel);

        if (rawVersionModel?.Downloads?.Client == null) yield break;

        var clientDownload = rawVersionModel.Downloads.Client;
        var jarPath = GamePathHelper.GetVersionJar(this.BasePath, id);


        if (File.Exists(jarPath))
        {
            if (string.IsNullOrEmpty(clientDownload.Sha1)) yield break;

            await using var jarFs = File.Open(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(jarFs));

            if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                yield break;
        }

        if (string.IsNullOrEmpty(clientDownload.Url))
            yield break;

        yield return new VersionJarDownloadInfo
        {
            CheckSum = clientDownload.Sha1 ?? string.Empty,
            FileName = $"{id}.jar",
            FileSize = clientDownload.Size,
            Path = Path.Combine(this.BasePath, GamePathHelper.GetGamePath(id)),
            Title = $"{id}.jar",
            Type = ResourceType.GameJar,
            Url = clientDownload.Url
        };
    }
}