using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeSortableGameVersionModel
{
    [JsonProperty("gameVersionPadded")] public string GameVersionPadded { get; set; }

    [JsonProperty("gameVersion")] public string GameVersion { get; set; }

    [JsonProperty("gameVersionReleaseDate")]
    public DateTime GameVersionReleaseDate { get; set; }

    [JsonProperty("gameVersionName")] public string GameVersionName { get; set; }
}