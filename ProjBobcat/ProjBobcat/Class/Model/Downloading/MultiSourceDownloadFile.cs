using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Model.Downloading;

public record DownloadUriInfo(string DownloadUri, int Weight);

public sealed class MultiSourceDownloadFile : AbstractDownloadBase
{
    public required IReadOnlyList<DownloadUriInfo> DownloadUris { get; init; }

    public override string GetDownloadUrl()
    {
        var totalWeight = this.DownloadUris.Sum(item => Math.Max(0, item.Weight));
        if (totalWeight == 0) throw new InvalidOperationException("No valid download URL was provided.");

        var selectedWeight = this.RetryCount % totalWeight;
        foreach (var item in this.DownloadUris)
        {
            selectedWeight -= Math.Max(0, item.Weight);
            if (selectedWeight < 0) return item.DownloadUri;
        }

        throw new InvalidOperationException("No valid download URL was provided.");
    }
}
