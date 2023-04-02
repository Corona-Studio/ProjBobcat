using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class LicenseInfo
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}

public class ModrinthProjectInfo : ModrinthProjectInfoBase
{
    [JsonPropertyName("id")] public string ProjectId { get; set; }

    [JsonPropertyName("team")] public string TeamId { get; set; }

    [JsonPropertyName("body")] public string Body { get; set; }

    [JsonPropertyName("body_url")] public string BodyUrl { get; set; }

    [JsonPropertyName("published")] public DateTime Published { get; set; }

    [JsonPropertyName("updated")] public DateTime Updated { get; set; }

    [JsonPropertyName("status")] public string Status { get; set; }

    [JsonPropertyName("moderator_message")]
    public string ModeratorMessage { get; set; }

    [JsonPropertyName("license")] public LicenseInfo License { get; set; }

    [JsonPropertyName("followers")] public int Followers { get; set; }

    [JsonPropertyName("issues_url")] public string IssuesUrl { get; set; }

    [JsonPropertyName("source_url")] public string SourceUrl { get; set; }

    [JsonPropertyName("wiki_url")] public string WikiUrl { get; set; }

    [JsonPropertyName("discord_url")] public string DiscordUrl { get; set; }

    [JsonPropertyName("donation_urls")] public JsonElement[] DonationUrls { get; set; }
}

[JsonSerializable(typeof(ModrinthProjectInfo))]
partial class ModrinthProjectInfoContext : JsonSerializerContext
{
}