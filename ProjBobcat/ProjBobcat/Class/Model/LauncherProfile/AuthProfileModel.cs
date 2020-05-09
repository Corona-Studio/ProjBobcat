using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class AuthProfileModel
    {
        [JsonProperty("displayName")] public string DisplayName { get; set; }
    }
}