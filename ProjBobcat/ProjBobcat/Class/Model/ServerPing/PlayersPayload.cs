using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class PlayersPayload
{
    [JsonPropertyName("max")] public int Max { get; set; }

    [JsonPropertyName("online")] public int Online { get; set; }

    [JsonPropertyName("sample")] public Player[]? Sample { get; set; }
}