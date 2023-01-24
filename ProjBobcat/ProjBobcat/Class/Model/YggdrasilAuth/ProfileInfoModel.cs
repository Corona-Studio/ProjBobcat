using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ProfileInfoModel
{
    [JsonPropertyName("id")] public PlayerUUID UUID { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("properties")] public PropertyModel[] Properties { get; set; }
}