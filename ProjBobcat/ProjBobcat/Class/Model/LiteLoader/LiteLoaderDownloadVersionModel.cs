using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LiteLoader;

public class LiteLoaderDownloadVersionModel
{
    [JsonPropertyName("_id")] public string? Id { get; set; }
    [JsonPropertyName("mcversion")] public required string McVersion { get; set; }
    [JsonPropertyName("build")] public required LiteLoaderBuildModel Build { get; set; }
    [JsonPropertyName("hash")] public string? Hash { get; set; }
    [JsonPropertyName("type")] public required string Type { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("__v")] public int VersionLocker { get; set; }
}

[JsonSerializable(typeof(LiteLoaderDownloadVersionModel))]
public partial class LiteLoaderDownloadVersionModelContext : JsonSerializerContext;