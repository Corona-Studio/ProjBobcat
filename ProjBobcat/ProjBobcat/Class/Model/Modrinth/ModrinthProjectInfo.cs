using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Modrinth;

public class LicenseInfo
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}

public class ModrinthProjectInfo : ModrinthProjectInfoBase
{
    [JsonProperty("id")]
    public string ProjectId { get; set; }

    [JsonProperty("team")]
    public string TeamId { get; set; }

    [JsonProperty("body")]
    public string Body { get; set; }

    [JsonProperty("body_url")]
    public string BodyUrl { get; set; }

    [JsonProperty("published")]
    public DateTime Published { get; set; }

    [JsonProperty("updated")]
    public DateTime Updated { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("moderator_message")]
    public string ModeratorMessage { get; set; }

    [JsonProperty("license")]
    public LicenseInfo License { get; set; }

    [JsonProperty("followers")]
    public int Followers { get; set; }

    [JsonProperty("issues_url")]
    public string IssuesUrl { get; set; }

    [JsonProperty("source_url")]
    public string SourceUrl { get; set; }

    [JsonProperty("wiki_url")]
    public string WikiUrl { get; set; }

    [JsonProperty("discord_url")]
    public string DiscordUrl { get; set; }

    [JsonProperty("donation_urls")]
    public List<object> DonationUrls { get; set; }
}