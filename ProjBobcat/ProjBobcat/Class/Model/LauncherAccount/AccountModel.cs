using System;
using System.Text.Json.Serialization;
using ProjBobcat.Class.Model.LauncherProfile;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class AccountModel
{
    [JsonPropertyName("accessToken")] public required string AccessToken { get; set; }

    [JsonPropertyName("accessTokenExpiresAt")]
    public DateTime AccessTokenExpiresAt { get; set; }

    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("cape")] public string? Cape { get; set; }

    [JsonPropertyName("eligibleForMigration")]
    public bool EligibleForMigration { get; set; }

    [JsonPropertyName("hasMultipleProfiles")]
    public bool HasMultipleProfiles { get; set; }

    [JsonPropertyName("legacy")] public bool Legacy { get; set; }

    [JsonPropertyName("localId")] public required string LocalId { get; set; }

    [JsonPropertyName("minecraftProfile")] public AccountProfileModel? MinecraftProfile { get; set; }

    [JsonPropertyName("persistent")] public bool Persistent { get; set; }

    [JsonPropertyName("remoteId")] public required string RemoteId { get; set; }

    [JsonPropertyName("type")] public required string Type { get; set; }

    [JsonPropertyName("userProperites")] public AuthPropertyModel[]? UserProperites { get; set; }

    [JsonPropertyName("username")] public required string Username { get; set; }

    [JsonPropertyName("__id")] public Guid Id { get; set; }
}