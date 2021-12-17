using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing;

public class ServerPingModInfo
{
    [JsonProperty("type")] public string Type { get; set; }

    [JsonProperty("modList")] public List<ModInfo> ModList { get; set; }
}

public class ModInfo
{
    [JsonProperty("modid")] public string ModId { get; set; }


    [JsonProperty("version")] public string Version { get; set; }
}