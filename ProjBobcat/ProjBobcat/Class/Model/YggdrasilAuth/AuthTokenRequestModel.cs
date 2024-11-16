using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class AuthTokenRequestModel
{
    [JsonPropertyName("accessToken")] public required string AccessToken { get; init; }

    [JsonPropertyName("clientToken")] public required string ClientToken { get; init; }
}

[JsonSerializable(typeof(AuthTokenRequestModel))]
partial class AuthTokenRequestModelContext : JsonSerializerContext;