using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.ServerPing
{
    public class PlayersPayload
    {
        [JsonProperty("max")]
        public int Max { get; set; }

        [JsonProperty("online")]
        public int Online { get; set; }

        [JsonProperty("sample")]
        public List<Player> Sample { get; set; }
    }
}