using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectInfoSearchResult : ModrinthProjectInfoBase
{
    [JsonProperty("project_id")]
    public string ProjectId { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("follows")]
    public int Follows { get; set; }

    [JsonProperty("date_created")]
    public DateTime DateCreated { get; set; }

    [JsonProperty("date_modified")]
    public DateTime DateModified { get; set; }

    [JsonProperty("latest_version")]
    public string LatestVersion { get; set; }

    [JsonProperty("license")]
    public string License { get; set; }
}