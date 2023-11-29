using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeAddonAuthorInfo
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("projectId")] public int ProjectId { get; set; }

    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("projectTitleId")] public JsonElement ProjectTitleId { get; set; }

    [JsonPropertyName("projectTitleTitle")]
    public JsonElement ProjectTitleTitle { get; set; }

    [JsonPropertyName("userId")] public int UserId { get; set; }

    [JsonPropertyName("twitchId")] public int? TwitchId { get; set; }
}