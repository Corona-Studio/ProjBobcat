using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFeaturedAddonModel
{
    [JsonProperty("featured")] public List<CurseForgeAddonInfo> Featured { get; set; }

    [JsonProperty("popular")] public List<CurseForgeAddonInfo> Popular { get; set; }

    [JsonProperty("recentlyUpdated")] public List<CurseForgeAddonInfo> RecentlyUpdated { get; set; }
}