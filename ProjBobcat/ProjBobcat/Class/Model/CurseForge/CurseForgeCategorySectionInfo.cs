using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeCategorySectionInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("gameId")] public int GameId { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("packageType")] public int PackageType { get; set; }

    [JsonPropertyName("path")] public string? Path { get; set; }

    [JsonPropertyName("initialInclusionPattern")]
    public string? InitialInclusionPattern { get; set; }

    [JsonPropertyName("extraIncludePattern")]
    public JsonElement ExtraIncludePattern { get; set; }

    [JsonPropertyName("gameCategoryId")] public int GameCategoryId { get; set; }
}