using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthModPackIndexModel
{
    [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; }

    [JsonPropertyName("game")] public string? Game { get; set; }

    [JsonPropertyName("versionId")] public string? VersionId { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("files")] public ModrinthModPackFileModel[] Files { get; set; }

    [JsonPropertyName("dependencies")] public Dictionary<string, string> Dependencies { get; set; }
}

[JsonSerializable(typeof(ModrinthModPackIndexModel))]
partial class ModrinthModPackIndexModelContext : JsonSerializerContext
{
}