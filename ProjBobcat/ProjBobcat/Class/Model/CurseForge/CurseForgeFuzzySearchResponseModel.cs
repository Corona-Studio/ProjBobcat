using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFuzzySearchFileModel
{
    [JsonPropertyName("id")] public long Id { get; set; }

    [JsonPropertyName("file")] public CurseForgeLatestFileModel? File { get; set; }
}

public class CurseForgeFuzzySearchResponseModel
{
    [JsonPropertyName("isCacheBuilt")] public bool IsCacheBuilt { get; set; }

    [JsonPropertyName("exactMatches")] public CurseForgeFuzzySearchFileModel[]? ExactMatches { get; set; }

    [JsonPropertyName("exactFingerprints")]
    public long[]? ExactFingerprints { get; set; }

    [JsonPropertyName("partialMatches")] public CurseForgeFuzzySearchFileModel[]? PartialMatches { get; set; }

    [JsonPropertyName("partialMatchFingerprints")]
    public JsonElement? PartialMatchFingerprints { get; set; }

    [JsonPropertyName("installedFingerprints")]
    public long[]? InstalledFingerprints { get; set; }

    [JsonPropertyName("unmatchedFingerprints")]
    public long[]? UnmatchedFingerprints { get; set; }
}