using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricLibraries
{
    [JsonProperty("client")] public List<Library> Client { get; set; }
    [JsonProperty("common")] public List<Library> Common { get; set; }
    [JsonProperty("server")] public List<Library> Server { get; set; }
}

public class FabricLauncherMeta
{
    [JsonProperty("version")] public int Version { get; set; }
    [JsonProperty("libraries")] public FabricLibraries Libraries { get; set; }
    [JsonProperty("mainClass")] public object MainClass { get; set; }
}