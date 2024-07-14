using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricFileInfo
{
    [JsonPropertyName("file")] public string? File { get; set; }
}

public class ModUpdater
{
    [JsonPropertyName("strategy")] public string? Strategy { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }
}

public class Custom
{
    [JsonPropertyName("modUpdater")] public ModUpdater? ModUpdater { get; set; }
}

public class FabricModInfoModel
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("environment")] public string? Environment { get; set; }

    [JsonPropertyName("entrypoints")] public IReadOnlyDictionary<string, JsonElement[]>? Entrypoints { get; set; }

    [JsonPropertyName("custom")] public Custom? Custom { get; set; }

    [JsonPropertyName("depends")] public IReadOnlyDictionary<string, JsonElement>? Depends { get; set; }

    [JsonPropertyName("recommends")] public IReadOnlyDictionary<string, string>? Recommends { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("icon")] public string? Icon { get; set; }

    [JsonPropertyName("authors")] public JsonElement[] Authors { get; set; } = [];

    [JsonPropertyName("contacts")] public IReadOnlyDictionary<string, string>? Contacts { get; set; }

    [JsonPropertyName("jars")] public FabricFileInfo[]? Jars { get; set; }
}

[JsonSerializable(typeof(FabricModInfoModel))]
partial class FabricModInfoModelContext : JsonSerializerContext;