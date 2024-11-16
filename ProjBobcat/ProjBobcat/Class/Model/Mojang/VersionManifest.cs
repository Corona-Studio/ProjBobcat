using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Mojang;

public class Latest
{
    [JsonPropertyName("release")] public required string Release { get; init; }
    [JsonPropertyName("snapshot")] public required string Snapshot { get; init; }
}

public class VersionManifestVersionsModel
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; }
}

public class VersionManifest
{
    [JsonPropertyName("latest")] public required Latest Latest { get; init; }

    [JsonPropertyName("versions")] public VersionManifestVersionsModel[]? Versions { get; set; }
}

[JsonSerializable(typeof(VersionManifest))]
public partial class VersionManifestContext : JsonSerializerContext;