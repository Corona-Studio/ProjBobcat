using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class AuthRefreshRequestModel
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }

        [JsonProperty("clientToken")] public string ClientToken { get; set; }

        [JsonProperty("requestUser")] public bool RequestUser { get; set; }

        [JsonProperty("selectedProfile")] public ProfileInfoModel SelectedProfile { get; set; }
    }
}