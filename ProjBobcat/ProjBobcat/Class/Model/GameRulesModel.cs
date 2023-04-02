using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class GameRules
{
    [JsonPropertyName("action")] public string Action { get; set; }

    [JsonPropertyName("features")] public Dictionary<string, bool> Features { get; set; }
}

[JsonSerializable(typeof(GameRules))]
[JsonSerializable(typeof(GameRules[]))]
partial class GameRulesContext : JsonSerializerContext
{
}