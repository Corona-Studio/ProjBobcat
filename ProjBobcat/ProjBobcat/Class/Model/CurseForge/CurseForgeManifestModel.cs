using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeManifestModel
{
    [JsonPropertyName("minecraft")] public required CurseForgeMineCraftModel MineCraft { get; init; }

    [JsonPropertyName("manifestType")] public string? ManifestType { get; set; }

    [JsonPropertyName("manifestVersion")] public int ManifestVersion { get; set; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("version")] public required string Version { get; init; }

    [JsonPropertyName("author")] public string? Author { get; set; }

    [JsonPropertyName("files")] public CurseForgeFileModel[]? Files { get; init; }

    [JsonPropertyName("overrides")] public string? Overrides { get; set; }
}

[JsonSerializable(typeof(CurseForgeManifestModel))]
public partial class CurseForgeManifestModelContext : JsonSerializerContext;