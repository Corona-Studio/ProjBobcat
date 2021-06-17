using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeAddonInfo
    {
        [JsonProperty("id")] public int Id { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("authors")] public List<CurseForgeAddonAuthorInfo> Authors { get; set; }

        [JsonProperty("attachments")] public List<CurseForgeAttachmentInfo> Attachments { get; set; }

        [JsonProperty("websiteUrl")] public string WebsiteUrl { get; set; }

        [JsonProperty("gameId")] public int GameId { get; set; }

        [JsonProperty("summary")] public string Summary { get; set; }

        [JsonProperty("defaultFileId")] public int DefaultFileId { get; set; }

        [JsonProperty("downloadCount")] public double DownloadCount { get; set; }

        [JsonProperty("latestFiles")] public List<CurseForgeLatestFileModel> LatestFiles { get; set; }

        [JsonProperty("categories")] public List<CurseForgeCategoryInfo> Categories { get; set; }

        [JsonProperty("status")] public int Status { get; set; }

        [JsonProperty("primaryCategoryId")] public int PrimaryCategoryId { get; set; }

        [JsonProperty("categorySection")] public CurseForgeCategorySectionInfo CategorySection { get; set; }

        [JsonProperty("slug")] public string Slug { get; set; }

        [JsonProperty("gameVersionLatestFiles")]
        public List<CurseForgeGameVersionLatestFiles> GameVersionLatestFiles { get; set; }

        [JsonProperty("isFeatured")] public bool IsFeatured { get; set; }

        [JsonProperty("popularityScore")] public double PopularityScore { get; set; }

        [JsonProperty("gamePopularityRank")] public int GamePopularityRank { get; set; }

        [JsonProperty("primaryLanguage")] public string PrimaryLanguage { get; set; }

        [JsonProperty("gameSlug")] public string GameSlug { get; set; }

        [JsonProperty("gameName")] public string GameName { get; set; }

        [JsonProperty("portalName")] public string PortalName { get; set; }

        [JsonProperty("dateModified")] public DateTime DateModified { get; set; }

        [JsonProperty("dateCreated")] public DateTime DateCreated { get; set; }

        [JsonProperty("dateReleased")] public DateTime DateReleased { get; set; }

        [JsonProperty("isAvailable")] public bool IsAvailable { get; set; }

        [JsonProperty("isExperiemental")] public bool IsExperiemental { get; set; }
    }
}