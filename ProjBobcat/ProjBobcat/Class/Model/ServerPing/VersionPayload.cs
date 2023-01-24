using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class VersionPayload
{
    [JsonPropertyName("protocol")] public int Protocol { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }
}