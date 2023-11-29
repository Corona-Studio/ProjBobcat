using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class AccountProfileModel
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }
}