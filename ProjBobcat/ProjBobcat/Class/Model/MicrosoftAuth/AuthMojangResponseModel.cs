using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class AuthMojangResponseModel
{
    [JsonPropertyName("username")] public string? UserName { get; set; }

    [JsonPropertyName("roles")] public JsonElement[]? Roles { get; set; }

    [JsonPropertyName("access_token")] public required string AccessToken { get; set; }

    [JsonPropertyName("token_type")] public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")] public long ExpiresIn { get; set; }
}

[JsonSerializable(typeof(AuthMojangResponseModel))]
partial class AuthMojangResponseModelContext : JsonSerializerContext;