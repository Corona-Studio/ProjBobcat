using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class VersionPayload
{
    [JsonPropertyName("protocol")] public int Protocol { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }
}