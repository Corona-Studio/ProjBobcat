using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFeaturedAddonModel
{
    [JsonPropertyName("featured")]
    public CurseForgeAddonInfo[] Featured { get; set; } = Array.Empty<CurseForgeAddonInfo>();

    [JsonPropertyName("popular")]
    public CurseForgeAddonInfo[] Popular { get; set; } = Array.Empty<CurseForgeAddonInfo>();

    [JsonPropertyName("recentlyUpdated")]
    public CurseForgeAddonInfo[] RecentlyUpdated { get; set; } = Array.Empty<CurseForgeAddonInfo>();
}