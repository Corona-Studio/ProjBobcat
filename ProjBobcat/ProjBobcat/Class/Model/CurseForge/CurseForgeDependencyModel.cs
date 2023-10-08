using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeDependencyModelComparer : IEqualityComparer<CurseForgeDependencyModel>
{
    public bool Equals(CurseForgeDependencyModel? x, CurseForgeDependencyModel? y)
    {
        if (x == null && y == null) return false;
        if (x == null || y == null) return false;

        return x.ModId == y.ModId;
    }

    public int GetHashCode(CurseForgeDependencyModel obj)
    {
        return obj.ModId.GetHashCode();
    }
}

public class CurseForgeDependencyModel
{
    [JsonPropertyName("modId")] public long ModId { get; set; }
    [JsonPropertyName("relationType")] public int RelationType { get; set; }
}