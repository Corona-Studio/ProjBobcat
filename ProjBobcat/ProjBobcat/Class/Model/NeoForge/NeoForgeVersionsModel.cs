using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.NeoForge;

public class NeoForgeVersionModel
{
    [JsonPropertyName("rawVersion")] public required string RawVersion { get; init; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("mcversion")] public required string MineCraftVersion { get; init; }
}

[JsonSerializable(typeof(NeoForgeVersionModel))]
public partial class NeoForgeVersionsModelContext : JsonSerializerContext;