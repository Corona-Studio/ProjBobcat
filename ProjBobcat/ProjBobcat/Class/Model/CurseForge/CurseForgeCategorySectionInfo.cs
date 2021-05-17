using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge
{
    public class CurseForgeCategorySectionInfo
    {
        [JsonProperty("id")] public int Id { get; set; }

        [JsonProperty("gameId")] public int GameId { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("packageType")] public int PackageType { get; set; }

        [JsonProperty("path")] public string Path { get; set; }

        [JsonProperty("initialInclusionPattern")]
        public string InitialInclusionPattern { get; set; }

        [JsonProperty("extraIncludePattern")] public object ExtraIncludePattern { get; set; }

        [JsonProperty("gameCategoryId")] public int GameCategoryId { get; set; }
    }
}