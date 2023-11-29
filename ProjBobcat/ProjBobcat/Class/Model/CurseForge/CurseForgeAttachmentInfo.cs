using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeAttachmentInfo
{
    [JsonPropertyName("projectId")] public int ProjectId { get; set; }

    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("status")] public int Status { get; set; }
}