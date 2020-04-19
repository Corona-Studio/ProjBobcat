using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class Agent
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("version")] public int Version { get; set; }
    }

    public class AuthRequestModel
    {
        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("password")] public string Password { get; set; }

        [JsonProperty("clientToken")] public string ClientToken { get; set; }

        [JsonProperty("requestUser")] public bool RequestUser { get; set; }

        [JsonProperty("agent")]
        public Agent Agent { get; set; } = new Agent
        {
            Name = "Minecraft",
            Version = 1
        };
    }
}