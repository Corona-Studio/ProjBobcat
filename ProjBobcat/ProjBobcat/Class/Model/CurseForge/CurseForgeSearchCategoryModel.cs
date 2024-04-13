using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeSearchCategoryModel
{
    [JsonPropertyName("id")] public int? Id { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("slug")] public string? Slug { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    [JsonPropertyName("dateModified")] public DateTime? DateModified { get; set; }

    [JsonPropertyName("parentCategoryId")] public int? ParentCategoryId { get; set; }

    [JsonPropertyName("rootCategoryId")] public int? RootCategoryId { get; set; }

    [JsonPropertyName("gameId")] public int? GameId { get; set; }

    [JsonPropertyName("classId")] public int? ClassId { get; set; }

    [JsonPropertyName("isClass")] public bool? IsClass { get; set; }
}