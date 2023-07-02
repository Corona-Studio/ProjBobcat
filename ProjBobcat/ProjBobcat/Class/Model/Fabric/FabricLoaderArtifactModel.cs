using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricLoaderArtifactModel
{
    [JsonPropertyName("loader")] public FabricArtifactModel Loader { get; set; }
    [JsonPropertyName("intermediary")] public FabricArtifactModel Intermediary { get; set; }
    [JsonPropertyName("launcherMeta")] public FabricLauncherMeta LauncherMeta { get; set; }
}

[JsonSerializable(typeof(FabricLoaderArtifactModel[]))]
public partial class FabricLoaderArtifactModelContext : JsonSerializerContext {}