using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeModLoaderModel
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("primary")] public bool IsPrimary { get; set; }
}

public class CurseForgeMineCraftModel
{
    [JsonPropertyName("version")] public required string Version { get; init; }

    [JsonPropertyName("modLoaders")] public CurseForgeModLoaderModel[]? ModLoaders { get; init; }
}