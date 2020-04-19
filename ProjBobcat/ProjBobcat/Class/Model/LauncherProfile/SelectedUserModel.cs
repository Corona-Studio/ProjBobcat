using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class SelectedUserModel
    {
        [JsonProperty("account")] public string Account { get; set; }

        [JsonProperty("profile")] public string Profile { get; set; }
    }
}