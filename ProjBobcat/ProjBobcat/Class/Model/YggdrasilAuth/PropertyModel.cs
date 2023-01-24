using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class PropertyModel
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("value")] public string Value { get; set; }

    [JsonPropertyName("signature")] public string Signature { get; set; }
}