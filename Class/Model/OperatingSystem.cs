using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    public class OperatingSystem
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("version")] public string Version { get; set; }
    }
}