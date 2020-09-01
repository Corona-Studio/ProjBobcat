using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Fabric
{
    public class FabricArtifactModel
    {
        [JsonProperty("gameVersion")] public string GameVersion { get; set; }
        [JsonProperty("separator")] public string Separator { get; set; }
        [JsonProperty("build")] public int BuildNum { get; set; }
        [JsonProperty("maven")] public string Maven { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("stable")] public bool IsStable { get; set; }
    }
}