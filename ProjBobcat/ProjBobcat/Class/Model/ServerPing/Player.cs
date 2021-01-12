using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing
{
    public class Player
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }
}