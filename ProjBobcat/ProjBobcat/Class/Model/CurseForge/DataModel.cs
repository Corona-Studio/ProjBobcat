using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class DataModel<T>
{
    [JsonPropertyName("data")] public T? Data { get; set; }
}

public class DataModelWithPagination<T> : DataModel<T>
{
    [JsonPropertyName("pagination")] public PaginationModel? Pagination { get; set; }
}