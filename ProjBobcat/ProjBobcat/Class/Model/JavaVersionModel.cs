using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    public class JavaVersionModel
    {
        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("majorVersion")]
        public int MajorVersion { get; set; }
    }
}