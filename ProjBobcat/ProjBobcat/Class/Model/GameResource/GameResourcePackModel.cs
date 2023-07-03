using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.GameResource;

public class Pack
{
    [JsonPropertyName("pack_format")] public int PackFormat { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class GameResourcePackModel
{
    [JsonPropertyName("pack")] public Pack? Pack { get; set; }
}

[JsonSerializable(typeof(GameResourcePackModel))]
partial class GameResourcePackModelContext : JsonSerializerContext
{
}