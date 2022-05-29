using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class PaginationModel
{
    [JsonProperty("index")]
    public int Index { get; set; }
    [JsonProperty("pageSize")]
    public int PageSize { get; set; }
    [JsonProperty("resultCount")]
    public int ResultCount { get; set; }
    [JsonProperty("totalCount")]
    public int TotalCount { get; set; }
}