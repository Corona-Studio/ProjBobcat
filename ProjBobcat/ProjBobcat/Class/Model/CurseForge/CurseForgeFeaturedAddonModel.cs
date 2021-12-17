using System.Collections.Generic;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFeaturedAddonModel
{
    public List<CurseForgeAddonInfo> Featured { get; set; }
    public List<CurseForgeAddonInfo> Popular { get; set; }
    public List<CurseForgeAddonInfo> RecentlyUpdated { get; set; }
}