using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Mojang;

public class UserProfileProperty
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("value")] public string Value { get; set; }
}

public class UserProfile
{
    public const string MojangUserMineCraftProfileUrl = "https://api.mojang.com/users/profiles/minecraft/";
    public const string MojangUserProfileUrl = "https://sessionserver.mojang.com/session/minecraft/profile/";

    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("properties")] public List<UserProfileProperty> Properties { get; set; }
}