using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.GameResource;

public class GameModInfoModel
{
    [JsonPropertyName("modid")]
    public string? ModId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("mcversion")]
    public string? McVersion { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("credits")]
    public string? Credits { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("updateUrl")]
    public string? UpdateUrl { get; set; }

    [JsonPropertyName("authorList")]
    public string[]? AuthorList { get; set; }

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("screenshots")]
    public string[]? Screenshots { get; set; }

    [JsonPropertyName("dependencies")]
    public string[]? Dependencies { get; set; }
}

[JsonSerializable(typeof(GameModInfoModel))]
[JsonSerializable(typeof(GameModInfoModel[]))]
partial class GameModInfoModelContext : JsonSerializerContext;