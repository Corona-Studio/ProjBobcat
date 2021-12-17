using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Mojang;

public class Latest
{
    [JsonProperty("release")] public string Release { get; set; }
    [JsonProperty("snapshot")] public string Snapshot { get; set; }
}

public class VersionManifestVersionsModel
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("url")] public string Url { get; set; }
    [JsonProperty("time")] public DateTime Time { get; set; }
    [JsonProperty("releaseTime")] public DateTime ReleaseTime { get; set; }
}

public class VersionManifest
{
    [JsonProperty("latest")] public Latest Latest { get; set; }

    [JsonProperty("versions")] public List<VersionManifestVersionsModel> Versions { get; set; }
}