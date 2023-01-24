using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectInfoSearchResult : ModrinthProjectInfoBase
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; }

    [JsonPropertyName("author")] public string Author { get; set; }

    [JsonPropertyName("follows")] public int Follows { get; set; }

    [JsonPropertyName("date_created")] public DateTime DateCreated { get; set; }

    [JsonPropertyName("date_modified")] public DateTime DateModified { get; set; }

    [JsonPropertyName("latest_version")] public string LatestVersion { get; set; }

    [JsonPropertyName("license")] public string License { get; set; }
}