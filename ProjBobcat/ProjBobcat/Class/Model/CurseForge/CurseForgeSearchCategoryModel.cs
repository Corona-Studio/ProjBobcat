using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeSearchCategoryModel
{
    [JsonProperty("id")] public int? Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("slug")] public string Slug { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("iconUrl")] public string IconUrl { get; set; }

    [JsonProperty("dateModified")] public DateTime? DateModified { get; set; }

    [JsonProperty("parentCategoryId")] public int? ParentCategoryId { get; set; }

    [JsonProperty("rootCategoryId")] public int? RootCategoryId { get; set; }

    [JsonProperty("gameId")] public int? GameId { get; set; }

    [JsonProperty("isClass")] public bool? IsClass { get; set; }
}