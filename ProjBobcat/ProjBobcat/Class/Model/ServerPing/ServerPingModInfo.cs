using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing
{
    public class ServerPingModInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("modList")]
        public List<object> ModList { get; set; }
    }
}