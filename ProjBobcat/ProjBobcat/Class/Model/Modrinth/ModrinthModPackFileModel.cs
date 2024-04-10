using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthModPackFileModel
{
    [JsonPropertyName("path")] public string? Path { get; set; }

    [JsonPropertyName("hashes")]
    public IReadOnlyDictionary<string, string> Hashes { get; set; } = ImmutableDictionary<string, string>.Empty;

    [JsonPropertyName("downloads")] public string[] Downloads { get; set; } = [];

    [JsonPropertyName("fileSize")] public long Size { get; set; }
}