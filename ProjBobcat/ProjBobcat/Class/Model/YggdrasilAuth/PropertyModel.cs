using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class PropertyModel
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("value")] public required string Value { get; init; }

    [JsonPropertyName("signature")] public string? Signature { get; init; }
}