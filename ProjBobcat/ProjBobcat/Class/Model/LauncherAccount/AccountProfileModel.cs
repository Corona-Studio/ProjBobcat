using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class AccountProfileModel
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }
}