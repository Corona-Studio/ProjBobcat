using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Mojang;

public class Latest
{
    [JsonPropertyName("release")] public string Release { get; set; }
    [JsonPropertyName("snapshot")] public string Snapshot { get; set; }
}

public class VersionManifestVersionsModel
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; }
}

public class VersionManifest
{
    [JsonPropertyName("latest")] public Latest Latest { get; set; }

    [JsonPropertyName("versions")] public VersionManifestVersionsModel[] Versions { get; set; }
}

[JsonSerializable(typeof(VersionManifest))]
public partial class VersionManifestContext : JsonSerializerContext
{
}