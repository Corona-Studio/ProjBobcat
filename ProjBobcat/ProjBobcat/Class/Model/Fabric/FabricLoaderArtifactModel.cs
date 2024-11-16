using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricLoaderArtifactModel
{
    [JsonPropertyName("loader")] public required FabricArtifactModel Loader { get; init; }
    [JsonPropertyName("intermediary")] public required FabricArtifactModel Intermediary { get; init; }
    [JsonPropertyName("launcherMeta")] public required FabricLauncherMeta LauncherMeta { get; init; }
}

[JsonSerializable(typeof(FabricLoaderArtifactModel))]
public partial class FabricLoaderArtifactModelContext : JsonSerializerContext;