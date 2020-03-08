using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class AuthInfoModel
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }

        [JsonProperty("profiles")] public Dictionary<string, AuthProfileModel> Profiles { get; set; }

        [JsonProperty("properties")] public List<AuthPropertyModel> Properties { get; set; }

        [JsonProperty("username")] public string UserName { get; set; }
    }
}