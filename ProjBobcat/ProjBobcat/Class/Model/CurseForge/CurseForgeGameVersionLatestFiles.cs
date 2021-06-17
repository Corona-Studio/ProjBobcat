using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeGameVersionLatestFiles
    {
        [JsonProperty("gameVersion")] public string GameVersion { get; set; }

        [JsonProperty("projectFileId")] public int ProjectFileId { get; set; }

        [JsonProperty("projectFileName")] public string ProjectFileName { get; set; }

        [JsonProperty("fileType")] public int FileType { get; set; }

        [JsonProperty("gameVersionFlavor")] public object GameVersionFlavor { get; set; }
    }
}