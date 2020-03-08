using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class AuthTokenRequestModel
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }

        [JsonProperty("clientToken")] public string ClientToken { get; set; }
    }
}