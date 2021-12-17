using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeAddonAuthorInfo
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("projectId")] public int ProjectId { get; set; }

    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("projectTitleId")] public object ProjectTitleId { get; set; }

    [JsonProperty("projectTitleTitle")] public object ProjectTitleTitle { get; set; }

    [JsonProperty("userId")] public int UserId { get; set; }

    [JsonProperty("twitchId")] public int? TwitchId { get; set; }
}