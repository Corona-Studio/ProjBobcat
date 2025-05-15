using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ProfileInfoModel
{
    [JsonPropertyName("id")] public required Guid Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("properties")] public PropertyModel[]? Properties { get; init; }
}