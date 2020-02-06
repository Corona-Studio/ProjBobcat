using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    public class GameRules
    {
        [JsonProperty("action")] public string Action { get; set; }

        [JsonProperty("features")] public Dictionary<string, bool> Features { get; set; }
    }
}