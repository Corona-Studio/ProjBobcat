using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LiteLoader
{
    public class LiteLoaderDownloadVersionModel
    {
        [JsonProperty("_id")] public string Id { get; set; }
        [JsonProperty("mcversion")] public string McVersion { get; set; }
        [JsonProperty("build")] public LiteLoaderBuildModel Build { get; set; }
        [JsonProperty("hash")] public string Hash { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("__v")] public int VersionLocker { get; set; }
    }
}