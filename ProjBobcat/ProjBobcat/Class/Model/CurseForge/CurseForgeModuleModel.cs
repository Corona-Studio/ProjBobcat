using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeModuleModel
    {
        [JsonProperty("foldername")]
        public string FolderName { get; set; }
        [JsonProperty("fingerprint")]
        public long Fingerprint { get; set; }
        [JsonProperty("type")]
        public int Type { get; set; }
    }
}