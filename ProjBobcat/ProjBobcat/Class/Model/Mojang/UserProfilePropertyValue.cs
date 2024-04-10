using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Mojang;

public class UrlModel
{
    [JsonPropertyName("url")] public required string Url { get; init; }
}

public class UserProfilePropertyValue
{
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("profileId")] public string? ProfileId { get; set; }
    [JsonPropertyName("profileName")] public string? ProfileName { get; set; }
    [JsonPropertyName("textures")] public IReadOnlyDictionary<string, UrlModel>? Textures { get; set; }
}

[JsonSerializable(typeof(UserProfilePropertyValue))]
public partial class UserProfilePropertyValueContext : JsonSerializerContext;