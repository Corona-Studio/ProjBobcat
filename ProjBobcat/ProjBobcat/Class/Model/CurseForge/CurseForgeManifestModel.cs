using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeManifestModel
    {
        [JsonProperty("minecraft")] public CurseForgeMineCraftModel MineCraft { get; set; }

        [JsonProperty("manifestType")] public string ManifestType { get; set; }

        [JsonProperty("manifestVersion")] public int ManifestVersion { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("version")] public string Version { get; set; }

        [JsonProperty("author")] public string Author { get; set; }

        [JsonProperty("files")] public List<CurseForgeFileModel> Files { get; set; }

        [JsonProperty("overrides")] public string Overrides { get; set; }
    }
}