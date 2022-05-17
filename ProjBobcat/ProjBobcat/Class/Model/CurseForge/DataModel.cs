using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class DataModel<T>
{
    [JsonProperty("data")] public T Data { get; set; }
}