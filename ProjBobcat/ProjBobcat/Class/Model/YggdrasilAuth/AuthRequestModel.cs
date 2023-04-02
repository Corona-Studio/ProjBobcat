using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class Agent
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("version")] public int Version { get; set; }
}

public class AuthRequestModel
{
    [JsonPropertyName("username")] public string Username { get; set; }

    [JsonPropertyName("password")] public string Password { get; set; }

    [JsonPropertyName("clientToken")] public string ClientToken { get; set; }

    [JsonPropertyName("requestUser")] public bool RequestUser { get; set; }

    [JsonPropertyName("agent")]
    public Agent Agent { get; set; } = new()
    {
        Name = "Minecraft",
        Version = 1
    };
}

[JsonSerializable(typeof(AuthRequestModel))]
partial class AuthRequestModelContext : JsonSerializerContext
{
}