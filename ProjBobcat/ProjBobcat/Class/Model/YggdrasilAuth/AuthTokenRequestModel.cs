using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class AuthTokenRequestModel
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; }

    [JsonPropertyName("clientToken")] public string ClientToken { get; set; }
}

[JsonSerializable(typeof(AuthTokenRequestModel))]
partial class AuthTokenRequestModelContext : JsonSerializerContext
{
}