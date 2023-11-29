using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class GameRules
{
    [JsonPropertyName("action")] public required string Action { get; init; }

    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, bool> Features { get; set; } = new Dictionary<string, bool>();
}

[JsonSerializable(typeof(GameRules))]
[JsonSerializable(typeof(GameRules[]))]
partial class GameRulesContext : JsonSerializerContext
{
}