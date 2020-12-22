using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeFileModel
    {
        [JsonProperty("projectID")] public long ProjectId { get; set; }

        [JsonProperty("fileID")] public long FileId { get; set; }

        [JsonProperty("required")] public bool Required { get; set; }
    }
}