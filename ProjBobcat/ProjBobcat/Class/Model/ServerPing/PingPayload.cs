using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing;

public class PingPayload
{
    [JsonProperty("version")] public VersionPayload Version { get; set; }

    [JsonProperty("players")] public PlayersPayload Players { get; set; }

    [JsonProperty("description")] public object Description { get; set; }

    [JsonProperty("modinfo")] public ServerPingModInfo ModInfo { get; set; }

    [JsonProperty("favicon")] public string Icon { get; set; }
}