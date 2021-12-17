using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeAttachmentInfo
{
    [JsonProperty("projectId")] public int ProjectId { get; set; }

    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("isDefault")] public bool IsDefault { get; set; }

    [JsonProperty("thumbnailUrl")] public string ThumbnailUrl { get; set; }

    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("status")] public int Status { get; set; }
}