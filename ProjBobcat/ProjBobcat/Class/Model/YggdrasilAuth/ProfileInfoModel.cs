using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ProfileInfoModel
{
    [JsonPropertyName("id")] public required PlayerUUID UUID { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("properties")] public PropertyModel[]? Properties { get; init; }
}