using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeSortableGameVersionModel
{
    [JsonPropertyName("gameVersionPadded")]
    public string? GameVersionPadded { get; set; }

    [JsonPropertyName("gameVersion")] public string? GameVersion { get; set; }

    [JsonPropertyName("gameVersionReleaseDate")]
    public DateTime GameVersionReleaseDate { get; set; }

    [JsonPropertyName("gameVersionName")] public string? GameVersionName { get; set; }
}