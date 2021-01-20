using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing
{
    public class PingPayload
    {
        /// <summary>
        ///     Protocol that the server is using and the given name
        /// </summary>
        [JsonProperty("version")]
        public VersionPayload Version { get; set; }

        [JsonProperty("players")] public PlayersPayload Players { get; set; }

        [JsonProperty("description")] public string Motd { get; set; }

        /// <summary>
        ///     Server icon, important to note that it's encoded in base 64
        /// </summary>
        [JsonProperty("favicon")]
        public string Icon { get; set; }
    }
}