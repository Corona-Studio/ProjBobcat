using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Fabric
{
    public class FabricMainClass
    {
        [JsonProperty("client")] public string Client { get; set; }
        [JsonProperty("server")] public string Server { get; set; }
    }

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
        [JsonProperty("mainClass")] public FabricMainClass MainClass { get; set; }
    }
}