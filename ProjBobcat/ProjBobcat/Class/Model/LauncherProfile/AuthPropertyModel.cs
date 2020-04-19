using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class AuthPropertyModel
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("profileId")] public string ProfileId { get; set; }

        [JsonProperty("userId")] public string UserId { get; set; }

        [JsonProperty("value")] public string Value { get; set; }
    }
}