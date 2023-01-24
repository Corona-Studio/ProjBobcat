using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class PaginationModel
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("pageSize")] public int PageSize { get; set; }

    [JsonPropertyName("resultCount")] public int ResultCount { get; set; }

    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
}