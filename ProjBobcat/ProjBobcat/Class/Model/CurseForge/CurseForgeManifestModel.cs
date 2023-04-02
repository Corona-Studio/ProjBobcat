using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeManifestModel
{
    [JsonPropertyName("minecraft")] public CurseForgeMineCraftModel MineCraft { get; set; }

    [JsonPropertyName("manifestType")] public string ManifestType { get; set; }

    [JsonPropertyName("manifestVersion")] public int ManifestVersion { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("version")] public string Version { get; set; }

    [JsonPropertyName("author")] public string Author { get; set; }

    [JsonPropertyName("files")] public CurseForgeFileModel[] Files { get; set; }

    [JsonPropertyName("overrides")] public string Overrides { get; set; }
}

[JsonSerializable(typeof(CurseForgeManifestModel))]
partial class CurseForgeManifestModelContext : JsonSerializerContext
{
}