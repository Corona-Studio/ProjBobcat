using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class AuthRefreshRequestModel
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; }

    [JsonPropertyName("clientToken")] public string ClientToken { get; set; }

    [JsonPropertyName("requestUser")] public bool RequestUser { get; set; }

    [JsonPropertyName("selectedProfile")] public ProfileInfoModel SelectedProfile { get; set; }
}

[JsonSerializable(typeof(AuthRefreshRequestModel))]
partial class AuthRefreshRequestModelContext : JsonSerializerContext
{
}