using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class AuthResponseModel
{
    [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }

    [JsonPropertyName("clientToken")] public string? ClientToken { get; set; }

    [JsonPropertyName("availableProfiles")]
    public ProfileInfoModel[]? AvailableProfiles { get; set; }

    [JsonPropertyName("selectedProfile")] public ProfileInfoModel? SelectedProfile { get; set; }

    [JsonPropertyName("user")] public UserInfoModel? User { get; set; }
}

[JsonSerializable(typeof(AuthResponseModel))]
partial class AuthResponseModelContext : JsonSerializerContext;