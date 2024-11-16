using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class AuthRefreshRequestModel
{
    [JsonPropertyName("accessToken")] public required string AccessToken { get; init; }

    [JsonPropertyName("clientToken")] public required string ClientToken { get; init; }

    [JsonPropertyName("requestUser")] public required bool RequestUser { get; init; }

    [JsonPropertyName("selectedProfile")] public required ProfileInfoModel SelectedProfile { get; init; }
}

[JsonSerializable(typeof(AuthRefreshRequestModel))]
partial class AuthRefreshRequestModelContext : JsonSerializerContext;