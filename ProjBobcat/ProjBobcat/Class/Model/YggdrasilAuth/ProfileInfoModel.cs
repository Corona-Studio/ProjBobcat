using System;
using System.Text.Json.Serialization;
using ProjBobcat.JsonConverter;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ProfileInfoModel
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(GuidJsonConverter))]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("properties")]
    public PropertyModel[]? Properties { get; init; }
}