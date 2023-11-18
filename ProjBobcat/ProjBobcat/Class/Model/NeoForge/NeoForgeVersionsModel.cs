using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.NeoForge;

public class NeoForgeVersionModel
{
    [JsonPropertyName("rawVersion")] public string RawVersion { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("mcversion")] public string MineCraftVersion { get; set; }
}

[JsonSerializable(typeof(NeoForgeVersionModel))]
public partial class NeoForgeVersionsModelContext : JsonSerializerContext { }