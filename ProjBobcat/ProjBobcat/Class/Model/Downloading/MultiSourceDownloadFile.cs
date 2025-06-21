using System;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Downloading;

public record DownloadUriInfo(string DownloadUri, int Weight);

public sealed class MultiSourceDownloadFile : AbstractDownloadBase
{
    public required IReadOnlyList<DownloadUriInfo> DownloadUris { get; init; }

    private Lazy<List<string>> WeightedUriPool => new(() =>
    {
        var list = new List<string>();

        foreach (var t in this.DownloadUris)
        {
            for (var w = 0; w < t.Weight; w++)
                list.Add(t.DownloadUri);
        }

        return list;
    });

    public override string GetDownloadUrl()
    {
        var weightedList = WeightedUriPool.Value;
        return weightedList[RetryCount % weightedList.Count];
    }
}