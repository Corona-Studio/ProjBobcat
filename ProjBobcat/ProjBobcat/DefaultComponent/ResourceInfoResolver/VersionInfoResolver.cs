using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class VersionInfoResolver : ResolverBase
{
    public IReadOnlyList<DownloadUriInfo>? VersionUriRoots { get; init; }

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!checkLocalFiles) yield break;

        cancellationToken.ThrowIfCancellationRequested();

        var id = resolvedGame.RootVersion ?? resolvedGame.DirName;
        var versionJson = GamePathHelper.GetGameJsonPath(basePath, id);

        if (!File.Exists(versionJson)) yield break;

        await using var fs = File.OpenRead(versionJson);
        var rawVersionModel = await JsonSerializer.DeserializeAsync(fs, RawVersionModelContext.Default.RawVersionModel, cancellationToken).ConfigureAwait(false);

        if (rawVersionModel?.Downloads?.Client == null) yield break;

        var clientDownload = rawVersionModel.Downloads.Client;
        var jarPath = GamePathHelper.GetVersionJar(basePath, id);


        if (File.Exists(jarPath))
        {
            if (string.IsNullOrEmpty(clientDownload.Sha1)) yield break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // Version jars can be large
            
            try
            {
                await using var jarFs = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(jarFs, cts.Token).ConfigureAwait(false));

                if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                    yield break;
            }
            catch
            {
                // If verification fails, proceed to download
            }
        }

        if (string.IsNullOrEmpty(clientDownload.Url))
            yield break;

        var fallbackUrls = new List<DownloadUriInfo>(VersionUriRoots?.Count ?? 1);
        var initUrl = new DownloadUriInfo(clientDownload.Url, 1);
        
        if (VersionUriRoots is { Count: > 0 })
        {
            foreach (var uriRoot in VersionUriRoots)
            {
                var replacedUrl = initUrl with
                {
                    DownloadUri = initUrl.DownloadUri
                        .Replace("https://piston-meta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                        .Replace("https://launchermeta.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                        .Replace("https://launcher.mojang.com", uriRoot.DownloadUri, StringComparison.Ordinal)
                };

                fallbackUrls.Add(replacedUrl);
            }
        }
        else
        {
            fallbackUrls.Add(initUrl);
        }

        yield return new VersionJarDownloadInfo
        {
            CheckSum = clientDownload.Sha1 ?? string.Empty,
            FileName = $"{id}.jar",
            FileSize = clientDownload.Size,
            Path = Path.Combine(basePath, GamePathHelper.GetGamePath(id)),
            Title = $"{id}.jar",
            Type = ResourceType.GameJar,
            Urls = fallbackUrls
        };
    }
}