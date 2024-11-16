using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeAddonInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("authors")] public required CurseForgeAddonAuthorInfo[] Authors { get; init; }
    [JsonPropertyName("logo")] public CurseForgeAttachmentInfo? Logo { get; set; }

    [JsonPropertyName("screenshots")] public CurseForgeAttachmentInfo[] Screenshots { get; set; } = [];

    [JsonPropertyName("websiteUrl")] public string? WebsiteUrl { get; set; }

    [JsonPropertyName("gameId")] public int GameId { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("links")]
    public IReadOnlyDictionary<string, string> Links { get; set; } = ImmutableDictionary<string, string>.Empty;

    [JsonPropertyName("defaultFileId")] public int DefaultFileId { get; set; }
    [JsonPropertyName("releaseType")] public int ReleaseType { get; set; }

    [JsonPropertyName("downloadCount")] public double DownloadCount { get; set; }

    [JsonPropertyName("latestFiles")] public CurseForgeLatestFileModel[] LatestFiles { get; set; } = [];

    [JsonPropertyName("categories")] public CurseForgeCategoryInfo[] Categories { get; set; } = [];

    [JsonPropertyName("status")] public int Status { get; set; }

    [JsonPropertyName("primaryCategoryId")]
    public int PrimaryCategoryId { get; set; }

    [JsonPropertyName("categorySection")] public CurseForgeCategorySectionInfo? CategorySection { get; set; }

    [JsonPropertyName("slug")] public string? Slug { get; set; }

    [JsonPropertyName("gameVersionLatestFiles")]
    public CurseForgeGameVersionLatestFiles[]? GameVersionLatestFiles { get; set; }

    [JsonPropertyName("isFeatured")] public bool IsFeatured { get; set; }

    [JsonPropertyName("gamePopularityRank")]
    public int GamePopularityRank { get; set; }

    [JsonPropertyName("primaryLanguage")] public string? PrimaryLanguage { get; set; }

    [JsonPropertyName("gameSlug")] public string? GameSlug { get; set; }

    [JsonPropertyName("gameName")] public string? GameName { get; set; }

    [JsonPropertyName("portalName")] public string? PortalName { get; set; }

    [JsonPropertyName("dateModified")] public DateTime DateModified { get; set; }

    [JsonPropertyName("dateCreated")] public DateTime DateCreated { get; set; }

    [JsonPropertyName("dateReleased")] public DateTime DateReleased { get; set; }

    [JsonPropertyName("isAvailable")] public bool IsAvailable { get; set; }

    [JsonPropertyName("isExperiemental")] public bool IsExperimental { get; set; }
}