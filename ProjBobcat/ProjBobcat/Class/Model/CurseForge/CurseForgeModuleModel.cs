using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeModuleModel
{
    [JsonPropertyName("foldername")] public string? FolderName { get; set; }

    [JsonPropertyName("fingerprint")] public long Fingerprint { get; set; }

    [JsonPropertyName("type")] public int Type { get; set; }
}