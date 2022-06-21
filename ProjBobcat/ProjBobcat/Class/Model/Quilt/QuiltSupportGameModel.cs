using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Quilt;

public class QuiltSupportGameModel
{
    [JsonProperty("version")] public string Version { get; set; }

    [JsonProperty("stable")] public bool Stable { get; set; }
}