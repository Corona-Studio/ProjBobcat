using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing
{
    public class VersionPayload
    {
        [JsonProperty("protocol")]
        public int Protocol { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}