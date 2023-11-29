using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeCategoryInfo
{
    [JsonPropertyName("categoryId")] public int CategoryId { get; set; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; set; }

    [JsonPropertyName("parentId")] public int ParentId { get; set; }

    [JsonPropertyName("rootId")] public int RootId { get; set; }

    [JsonPropertyName("projectId")] public int ProjectId { get; set; }

    [JsonPropertyName("avatarId")] public int AvatarId { get; set; }

    [JsonPropertyName("gameId")] public int GameId { get; set; }
}