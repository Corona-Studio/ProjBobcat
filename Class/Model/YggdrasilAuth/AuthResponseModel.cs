using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class AuthResponseModel
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }

        [JsonProperty("clientToken")] public string ClientToken { get; set; }

        [JsonProperty("availableProfiles")] public List<ProfileInfoModel> AvailableProfiles { get; set; }

        [JsonProperty("selectedProfile")] public ProfileInfoModel SelectedProfile { get; set; }

        [JsonProperty("user")] public UserInfoModel User { get; set; }
    }
}