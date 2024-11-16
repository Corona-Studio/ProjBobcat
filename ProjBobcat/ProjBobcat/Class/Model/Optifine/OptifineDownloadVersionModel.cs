using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Optifine;

public class OptifineDownloadVersionModel
{
    [JsonPropertyName("_id")] public required string Id { get; init; }

    [JsonPropertyName("mcversion")] public required string McVersion { get; init; }

    [JsonPropertyName("patch")] public required string Patch { get; init; }

    [JsonPropertyName("type")] public required string Type { get; init; }

    [JsonPropertyName("__v")] public required int VersionLocker { get; init; }

    [JsonPropertyName("filename")] public required string FileName { get; init; }
}

[JsonSerializable(typeof(OptifineDownloadVersionModel))]
public partial class OptifineDownloadVersionModelContext : JsonSerializerContext;