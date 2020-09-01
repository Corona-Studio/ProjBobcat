using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Optifine
{
    public class OptifineDownloadVersionModel
    {
        [JsonProperty("_id")] public string Id { get; set; }
        [JsonProperty("mcversion")] public string McVersion { get; set; }
        [JsonProperty("patch")] public string Patch { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("__v")] public int VersionLocker { get; set; }
        [JsonProperty("filename")] public string FileName { get; set; }
    }
}