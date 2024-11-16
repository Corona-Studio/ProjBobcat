using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class Agent
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("version")] public int Version { get; init; }
}

public class AuthRequestModel
{
    [JsonPropertyName("username")] public required string Username { get; init; }

    [JsonPropertyName("password")] public required string Password { get; init; }

    [JsonPropertyName("clientToken")] public required string ClientToken { get; init; }

    [JsonPropertyName("requestUser")] public bool RequestUser { get; init; }

    [JsonPropertyName("agent")]
    public Agent Agent { get; set; } = new()
    {
        Name = "Minecraft",
        Version = 1
    };
}

[JsonSerializable(typeof(AuthRequestModel))]
partial class AuthRequestModelContext : JsonSerializerContext;