using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge.API;

public class FeaturedQueryOptions
{
    public int GameId { get; set; }

    [JsonPropertyName("addonIds")] public required int[] AddonIds { get; init; }

    [JsonPropertyName("featuredCount")] public required int FeaturedCount { get; init; }

    [JsonPropertyName("popularCount")] public required int PopularCount { get; init; }

    [JsonPropertyName("updatedCount")] public required int UpdatedCount { get; init; }

    public static FeaturedQueryOptions Default => new()
    {
        AddonIds = [],
        FeaturedCount = 15,
        GameId = 432,
        PopularCount = 150,
        UpdatedCount = 150
    };
}

[JsonSerializable(typeof(FeaturedQueryOptions))]
partial class FeaturedQueryOptionsContext : JsonSerializerContext;