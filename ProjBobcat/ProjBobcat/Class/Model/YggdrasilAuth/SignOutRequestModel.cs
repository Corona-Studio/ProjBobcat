using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class SignOutRequestModel
{
    [JsonPropertyName("username")] public string Username { get; set; }

    [JsonPropertyName("password")] public string Password { get; set; }
}

[JsonSerializable(typeof(SignOutRequestModel))]
partial class SignOutRequestModelContext : JsonSerializerContext
{
}