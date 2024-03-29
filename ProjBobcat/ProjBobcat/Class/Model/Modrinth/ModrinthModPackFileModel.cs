﻿using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthModPackFileModel
{
    [JsonPropertyName("path")] public string? Path { get; set; }

    [JsonPropertyName("hashes")]
    public IReadOnlyDictionary<string, string> Hashes { get; set; } = new Dictionary<string, string>();

    [JsonPropertyName("downloads")] public string[] Downloads { get; set; } = [];

    [JsonPropertyName("fileSize")] public long Size { get; set; }
}