using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Optifine;

public class OptifineDownloadVersionModel
{
    [JsonPropertyName("_id")] public string Id { get; set; }
    [JsonPropertyName("mcversion")] public string McVersion { get; set; }
    [JsonPropertyName("patch")] public string Patch { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("__v")] public int VersionLocker { get; set; }
    [JsonPropertyName("filename")] public string FileName { get; set; }
}

[JsonSerializable(typeof(OptifineDownloadVersionModel[]))]
public partial class OptifineDownloadVersionModelContext : JsonSerializerContext {}