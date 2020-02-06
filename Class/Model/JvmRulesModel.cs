using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    public class JvmRules
    {
        [JsonProperty("action")] public string Action { get; set; }

        [JsonProperty("os")] public Dictionary<string, string> OperatingSystem { get; set; }
    }
}