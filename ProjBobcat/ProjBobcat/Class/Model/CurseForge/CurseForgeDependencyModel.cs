using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeDependencyModelComparer : IEqualityComparer<CurseForgeDependencyModel>
{
    public bool Equals(CurseForgeDependencyModel x, CurseForgeDependencyModel y)
    {
        return x.ModId == y.ModId;
    }

    public int GetHashCode([DisallowNull] CurseForgeDependencyModel obj)
    {
        return obj.ModId.GetHashCode();
    }
}

public class CurseForgeDependencyModel
{
    [JsonProperty("modId")] public int ModId { get; set; }
    [JsonProperty("relationType")] public int RelationType { get; set; }
}