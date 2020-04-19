using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class LauncherProfileModel
    {
        [JsonProperty("profiles")] public Dictionary<string, GameProfileModel> Profiles { get; set; }

        [JsonProperty("clientToken")] public string ClientToken { get; set; }

        [JsonProperty("authenticationDatabase")]
        public Dictionary<string, AuthInfoModel> AuthenticationDatabase { get; set; }

        [JsonProperty("selectedUser")] public SelectedUserModel SelectedUser { get; set; }

        [JsonProperty("launcherVersion")] public LauncherVersionModel LauncherVersion { get; set; }
    }
}