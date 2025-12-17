using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class GameLoggingInfoResolver : ResolverBase
{
    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!checkLocalFiles) yield break;
        if (resolvedGame.Logging?.Client?.File == null) yield break;
        if (string.IsNullOrEmpty(resolvedGame.Logging?.Client?.File.Url)) yield break;

        cancellationToken.ThrowIfCancellationRequested();

        var fileName = Path.GetFileName(resolvedGame.Logging.Client.File?.Url);

        if (string.IsNullOrEmpty(fileName)) yield break;

        var loggingPath = GamePathHelper.GetLoggingPath(basePath);
        var filePath = Path.Combine(loggingPath, fileName);

        if (File.Exists(filePath))
        {
            if (string.IsNullOrEmpty(resolvedGame.Logging?.Client?.File?.Sha1)) yield break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            try
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs, cts.Token).ConfigureAwait(false));

                if (computedHash.Equals(resolvedGame.Logging?.Client?.File?.Sha1, StringComparison.OrdinalIgnoreCase))
                    yield break;
            }
            catch
            {
                // If verification fails, proceed to download
            }
        }

        if (string.IsNullOrEmpty(resolvedGame.Logging?.Client?.File?.Url))
            yield break;

        yield return new GameLoggingDownloadInfo
        {
            CheckSum = resolvedGame.Logging?.Client?.File?.Sha1,
            FileName = fileName,
            FileSize = resolvedGame.Logging?.Client?.File?.Size ?? 0,
            Path = loggingPath,
            Title = fileName,
            Type = ResourceType.Logging,
            Urls = [new DownloadUriInfo(resolvedGame.Logging!.Client!.File!.Url!, 1)]
        };
    }
}