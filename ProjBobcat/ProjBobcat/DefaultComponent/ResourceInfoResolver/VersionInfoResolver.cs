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
        if (!CheckLocalFiles) yield break;

        var id = VersionInfo.RootVersion ?? VersionInfo.DirName;
        var versionJson = GamePathHelper.GetGameJsonPath(BasePath, id);

        if (!File.Exists(versionJson)) yield break;

        await using var fs = File.OpenRead(versionJson);
        var rawVersionModel = await JsonSerializer.DeserializeAsync(fs, RawVersionModelContext.Default.RawVersionModel);

        if (rawVersionModel?.Downloads?.Client == null) yield break;

        var clientDownload = rawVersionModel.Downloads.Client;
        var jarPath = GamePathHelper.GetVersionJar(BasePath, id);


        if (File.Exists(jarPath))
        {
            if (string.IsNullOrEmpty(clientDownload.Sha1)) yield break;

#if NET7_0_OR_GREATER
            await using var jarFs = File.Open(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var computedHash = (await SHA1.HashDataAsync(jarFs)).BytesToString();
#else
            var bytes = await File.ReadAllBytesAsync(jarPath);
            var computedHash = SHA1.HashData(bytes.AsSpan()).BytesToString();
#endif

            if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                yield break;
        }

        yield return new VersionJarDownloadInfo
        {
            CheckSum = clientDownload.Sha1,
            FileName = $"{id}.jar",
            FileSize = clientDownload.Size,
            Path = Path.Combine(BasePath, GamePathHelper.GetGamePath(id)),
            Title = $"{id}.jar",
            Type = ResourceType.GameJar,
            Url = clientDownload.Url
        };
    }
}