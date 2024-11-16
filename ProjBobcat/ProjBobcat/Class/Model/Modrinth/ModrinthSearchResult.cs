using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthSearchResult
{
    [JsonPropertyName("hits")] public ModrinthProjectInfoSearchResult[] Hits { get; set; } = [];

    [JsonPropertyName("offset")] public int Offset { get; set; }

    [JsonPropertyName("limit")] public int Limit { get; set; }

    [JsonPropertyName("total_hits")] public int TotalHits { get; set; }
}

[JsonSerializable(typeof(ModrinthSearchResult))]
partial class ModrinthSearchResultContext : JsonSerializerContext;