using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge.API;

public class FeaturedQueryOptions
{
    public int GameId { get; set; }

    [JsonPropertyName("addonIds")] public int[] AddonIds { get; set; }

    [JsonPropertyName("featuredCount")] public int FeaturedCount { get; set; }

    [JsonPropertyName("popularCount")] public int PopularCount { get; set; }

    [JsonPropertyName("updatedCount")] public int UpdatedCount { get; set; }

    public static FeaturedQueryOptions Default => new()
    {
        AddonIds = Array.Empty<int>(),
        FeaturedCount = 15,
        GameId = 432,
        PopularCount = 150,
        UpdatedCount = 150
    };
}

[JsonSerializable(typeof(FeaturedQueryOptions))]
partial class FeaturedQueryOptionsContext : JsonSerializerContext
{
}