using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeSearchCategoryModel
    {
        [JsonProperty("id")]
        public int? Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("slug")]
        public string Slug { get; set; }
        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }
        [JsonProperty("dateModified")]
        public DateTime? DateModified { get; set; }
        [JsonProperty("parentGameCategoryId")]
        public int? ParentGameCategoryId { get; set; }
        [JsonProperty("rootGameCategoryId")]
        public int? RootGameCategoryId { get; set; }
        [JsonProperty("gameId")]
        public int? GameId { get; set; }
    }
}