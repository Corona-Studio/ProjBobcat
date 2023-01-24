using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class AccountProfileModel
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }
}