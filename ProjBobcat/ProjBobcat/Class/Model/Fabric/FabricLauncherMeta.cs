using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricLibraries
{
    [JsonPropertyName("client")] public Library[] Client { get; set; }
    [JsonPropertyName("common")] public Library[] Common { get; set; }
    [JsonPropertyName("server")] public Library[] Server { get; set; }
}

public class FabricLauncherMeta
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("libraries")] public FabricLibraries Libraries { get; set; }
    [JsonPropertyName("mainClass")] public JsonElement MainClass { get; set; }
}