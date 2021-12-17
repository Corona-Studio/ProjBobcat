using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeDependencyModelComparer : IEqualityComparer<CurseForgeDependencyModel>
{
    public bool Equals(CurseForgeDependencyModel x, CurseForgeDependencyModel y)
    {
        return x.AddonId == y.AddonId;
    }

    public int GetHashCode([DisallowNull] CurseForgeDependencyModel obj)
    {
        return obj.AddonId.GetHashCode();
    }
}

public class CurseForgeDependencyModel
{
    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("addonId")] public int AddonId { get; set; }

    [JsonProperty("type")] public int Type { get; set; }

    [JsonProperty("fileId")] public int FileId { get; set; }
}