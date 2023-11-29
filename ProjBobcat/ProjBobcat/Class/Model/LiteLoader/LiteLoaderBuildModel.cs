using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LiteLoader;

public class LiteLoaderBuildModel
{
    [JsonPropertyName("tweakClass")] public required string TweakClass { get; set; }
    [JsonPropertyName("libraries")] public Library[] Libraries { get; set; } = [];
    [JsonPropertyName("stream")] public string? Stream { get; set; }
    [JsonPropertyName("file")] public string? File { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("md5")] public string? Md5 { get; set; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("srcJar")] public string? SrcJar { get; set; }
    [JsonPropertyName("mcpJar")] public string? McpJar { get; set; }
}