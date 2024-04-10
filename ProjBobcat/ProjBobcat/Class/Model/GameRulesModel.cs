using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class GameRules
{
    [JsonPropertyName("action")] public required string Action { get; init; }

    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, bool> Features { get; set; } = ImmutableDictionary<string, bool>.Empty;
}

[JsonSerializable(typeof(GameRules))]
[JsonSerializable(typeof(GameRules[]))]
partial class GameRulesContext : JsonSerializerContext;