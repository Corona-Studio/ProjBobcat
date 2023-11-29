using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class ServerPingModInfo
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("modList")] public ModInfo[]? ModList { get; set; }
}

public class ModInfo
{
    [JsonPropertyName("modid")] public string? ModId { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }
}