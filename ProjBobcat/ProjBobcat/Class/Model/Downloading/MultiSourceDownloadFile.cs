using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Model.Downloading;

public record DownloadUriInfo(string DownloadUri, int Weight);

public sealed class MultiSourceDownloadFile : AbstractDownloadBase
{
    public required IReadOnlyList<DownloadUriInfo> DownloadUris { get; init; }

    private Lazy<string[]> WeightedUriPool => new(() =>
    {
        var list = new string[this.DownloadUris.Sum(i => i.Weight)];
        var index = 0;

        foreach (var t in this.DownloadUris)
        {
            for (var w = 0; w < t.Weight; w++)
                list[index++] = t.DownloadUri;
        }

        return list;
    });

    public override string GetDownloadUrl()
    {
        var weightedList = WeightedUriPool.Value;
        return weightedList[RetryCount % weightedList.Length];
    }
}