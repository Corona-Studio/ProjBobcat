using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Fabric
{
    public class FabricLoaderArtifactModel
    {
        [JsonProperty("loader")] public FabricArtifactModel Loader { get; set; }
        [JsonProperty("intermediary")] public FabricArtifactModel Intermediary { get; set; }
        [JsonProperty("launcherMeta")] public FabricLauncherMeta LauncherMeta { get; set; }
    }
}