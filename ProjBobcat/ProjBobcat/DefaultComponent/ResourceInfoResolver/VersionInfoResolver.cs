﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
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
        ResolvedGameVersion resolvedGame)
    {
        if (!checkLocalFiles) yield break;

        var id = resolvedGame.RootVersion ?? resolvedGame.DirName;
        var versionJson = GamePathHelper.GetGameJsonPath(basePath, id);

        if (!File.Exists(versionJson)) yield break;

        await using var fs = File.OpenRead(versionJson);
        var rawVersionModel = await JsonSerializer.DeserializeAsync(fs, RawVersionModelContext.Default.RawVersionModel);

        if (rawVersionModel?.Downloads?.Client == null) yield break;

        var clientDownload = rawVersionModel.Downloads.Client;
        var jarPath = GamePathHelper.GetVersionJar(basePath, id);


        if (File.Exists(jarPath))
        {
            if (string.IsNullOrEmpty(clientDownload.Sha1)) yield break;

            await using var jarFs = File.Open(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(jarFs).ConfigureAwait(false));

            if (computedHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                yield break;
        }

        if (string.IsNullOrEmpty(clientDownload.Url))
            yield break;

        var fallbackUrls = new List<DownloadUriInfo> { new (clientDownload.Url, 1) };
        if (VersionUriRoots is { Count: > 0 })
        {
            var initUrl = fallbackUrls[0];
            fallbackUrls.Clear();

            foreach (var uriRoot in VersionUriRoots)
            {
                var replacedUrl = initUrl with
                {
                    DownloadUri = initUrl.DownloadUri
                        .Replace("https://piston-meta.mojang.com", uriRoot.DownloadUri)
                        .Replace("https://launchermeta.mojang.com", uriRoot.DownloadUri)
                        .Replace("https://launcher.mojang.com", uriRoot.DownloadUri)
                };

                fallbackUrls.Add(replacedUrl);
            }
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