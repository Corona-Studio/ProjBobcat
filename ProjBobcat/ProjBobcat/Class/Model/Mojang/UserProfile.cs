using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Mojang;

public class UserProfileProperty
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
}

public class UserProfile
{
    public const string MojangUserMineCraftProfileUrl = "https://api.mojang.com/users/profiles/minecraft/";
    public const string MojangUserProfileUrl = "https://sessionserver.mojang.com/session/minecraft/profile/";

    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("properties")] public UserProfileProperty[]? Properties { get; set; }
}

[JsonSerializable(typeof(UserProfile))]
public partial class UserProfileContext : JsonSerializerContext;