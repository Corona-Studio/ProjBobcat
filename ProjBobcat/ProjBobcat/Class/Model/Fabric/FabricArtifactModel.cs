using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricArtifactModel
{
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; set; }
    [JsonPropertyName("separator")] public string? Separator { get; set; }
    [JsonPropertyName("build")] public int BuildNum { get; set; }
    [JsonPropertyName("maven")] public required string Maven { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }
    [JsonPropertyName("stable")] public bool IsStable { get; set; }
}