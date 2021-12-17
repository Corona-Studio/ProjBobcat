using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeCategoryInfo
{
    [JsonProperty("categoryId")] public int CategoryId { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("avatarUrl")] public string AvatarUrl { get; set; }

    [JsonProperty("parentId")] public int ParentId { get; set; }

    [JsonProperty("rootId")] public int RootId { get; set; }

    [JsonProperty("projectId")] public int ProjectId { get; set; }

    [JsonProperty("avatarId")] public int AvatarId { get; set; }

    [JsonProperty("gameId")] public int GameId { get; set; }
}