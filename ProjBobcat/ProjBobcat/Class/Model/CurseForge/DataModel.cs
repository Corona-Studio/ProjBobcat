using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class DataModel<T>
{
    [JsonProperty("data")] public T Data { get; set; }
}

public class DataModelWithPagination<T> : DataModel<T>
{
    [JsonProperty("pagination")]
    public PaginationModel Pagination { get; set; }
}