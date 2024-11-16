using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class PingPayload
{
    [JsonPropertyName("version")] public VersionPayload? Version { get; set; }

    [JsonPropertyName("players")] public PlayersPayload? Players { get; set; }

    [JsonPropertyName("description")] public JsonElement Description { get; set; }

    [JsonPropertyName("modinfo")] public ServerPingModInfo? ModInfo { get; set; }

    [JsonPropertyName("favicon")] public string? Icon { get; set; }
}

[JsonSerializable(typeof(PingPayload))]
partial class PingPayloadContext : JsonSerializerContext;