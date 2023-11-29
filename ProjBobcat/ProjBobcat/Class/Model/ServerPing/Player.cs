using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.ServerPing;

public class Player
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }
}