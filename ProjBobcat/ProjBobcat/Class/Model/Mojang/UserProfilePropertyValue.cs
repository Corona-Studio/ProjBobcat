using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Mojang
{
    public class UrlModel
    {
        [JsonProperty("url")] public string Url { get; set; }
    }

    public class UserProfilePropertyValue
    {
        [JsonProperty("timestamp")] public long Timestamp { get; set; }
        [JsonProperty("profileId")] public string ProfileId { get; set; }
        [JsonProperty("profileName")] public string ProfileName { get; set; }
        [JsonProperty("textures")] public Dictionary<string, UrlModel> Textures { get; set; }
    }
}