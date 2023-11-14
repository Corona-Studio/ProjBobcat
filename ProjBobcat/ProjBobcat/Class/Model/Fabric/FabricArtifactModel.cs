using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricArtifactModel
{
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; set; }
    [JsonPropertyName("separator")] public string? Separator { get; set; }
    [JsonPropertyName("build")] public int BuildNum { get; set; }
    [JsonPropertyName("maven")] public string Maven { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; }
    [JsonPropertyName("stable")] public bool IsStable { get; set; }
}