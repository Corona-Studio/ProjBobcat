using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeModLoaderModel
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("primary")] public bool IsPrimary { get; set; }
}

public class CurseForgeMineCraftModel
{
    [JsonProperty("version")] public string Version { get; set; }

    [JsonProperty("modLoaders")] public List<CurseForgeModLoaderModel> ModLoaders { get; set; }
}