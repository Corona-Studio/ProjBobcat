using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class SignOutRequestModel
{
    [JsonPropertyName("username")] public required string Username { get; init; }

    [JsonPropertyName("password")] public required string Password { get; init; }
}

[JsonSerializable(typeof(SignOutRequestModel))]
partial class SignOutRequestModelContext : JsonSerializerContext;