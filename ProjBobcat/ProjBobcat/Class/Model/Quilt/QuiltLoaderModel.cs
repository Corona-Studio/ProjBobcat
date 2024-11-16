using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Quilt;

public class QuiltLoaderModel
{
    [JsonPropertyName("separator")] public string? Separator { get; set; }

    [JsonPropertyName("build")] public int Build { get; set; }

    [JsonPropertyName("maven")] public string? Maven { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }
}

[JsonSerializable(typeof(QuiltLoaderModel))]
public partial class QuiltLoaderModelContext : JsonSerializerContext;