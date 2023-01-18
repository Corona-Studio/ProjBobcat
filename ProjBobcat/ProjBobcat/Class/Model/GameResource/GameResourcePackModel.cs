using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.GameResource;

public class Pack
{
    [JsonProperty("pack_format")] public int PackFormat { get; set; }

    [JsonProperty("description")] public string? Description { get; set; }
}

public class GameResourcePackModel
{
    [JsonProperty("pack")] public Pack? Pack { get; set; }
}