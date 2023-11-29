using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeGameVersionLatestFiles
{
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; set; }

    [JsonPropertyName("projectFileId")] public int ProjectFileId { get; set; }

    [JsonPropertyName("projectFileName")] public string? ProjectFileName { get; set; }

    [JsonPropertyName("fileType")] public int FileType { get; set; }

    [JsonPropertyName("gameVersionFlavor")]
    public JsonElement GameVersionFlavor { get; set; }
}