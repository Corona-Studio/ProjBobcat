using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectInfoBase
{
    [JsonPropertyName("project_type")] public required string ProjectType { get; set; }

    [JsonPropertyName("slug")] public string? Slug { get; set; }

    [JsonPropertyName("title")] public required string Title { get; set; }

    [JsonPropertyName("description")] public required string Description { get; set; }

    [JsonPropertyName("categories")] public string[] Categories { get; set; } = [];

    [JsonPropertyName("versions")] public string[]? Versions { get; set; }

    [JsonPropertyName("downloads")] public int Downloads { get; set; }

    [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }

    [JsonPropertyName("client_side")] public string? ClientSide { get; set; }

    [JsonPropertyName("server_side")] public string? ServerSide { get; set; }

    [JsonPropertyName("gallery")] public JsonElement[] Gallery { get; set; } = [];
}