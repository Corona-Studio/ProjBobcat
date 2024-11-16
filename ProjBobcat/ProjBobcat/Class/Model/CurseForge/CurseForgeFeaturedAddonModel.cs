using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFeaturedAddonModel
{
    [JsonPropertyName("featured")] public CurseForgeAddonInfo[] Featured { get; set; } = [];

    [JsonPropertyName("popular")] public CurseForgeAddonInfo[] Popular { get; set; } = [];

    [JsonPropertyName("recentlyUpdated")] public CurseForgeAddonInfo[] RecentlyUpdated { get; set; } = [];
}