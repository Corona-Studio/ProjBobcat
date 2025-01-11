using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Downloading;

public sealed class MultiSourceDownloadFile : AbstractDownloadBase
{
    public required IReadOnlyList<string> DownloadUris { get; init; }

    public override string GetDownloadUrl() => DownloadUris[RetryCount % DownloadUris.Count];
}