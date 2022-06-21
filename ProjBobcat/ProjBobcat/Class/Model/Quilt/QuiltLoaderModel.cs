using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Quilt;

public class QuiltLoaderModel
{
    [JsonProperty("separator")] public string Separator { get; set; }

    [JsonProperty("build")] public int Build { get; set; }

    [JsonProperty("maven")] public string Maven { get; set; }

    [JsonProperty("version")] public string Version { get; set; }
}