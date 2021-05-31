using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeDependencyModel
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("addonId")]
        public int AddonId { get; set; }
        [JsonProperty("type")]
        public int Type { get; set; }
        [JsonProperty("fileId")]
        public int FileId { get; set; }
    }
}