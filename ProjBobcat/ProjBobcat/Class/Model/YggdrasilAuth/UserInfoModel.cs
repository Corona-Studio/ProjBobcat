using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class UserInfoModel
{
    [JsonPropertyName("id")] public PlayerUUID UUID { get; set; }

    [JsonPropertyName("username")] public string? UserName { get; set; }

    [JsonPropertyName("properties")] public PropertyModel[]? Properties { get; set; }
}