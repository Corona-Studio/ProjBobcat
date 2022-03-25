using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectDependencyInfo
{
    [JsonProperty("projects")]
    public List<ModrinthProjectInfo> Projects { get; set; }

    [JsonProperty("versions")]
    public List<ModrinthVersionInfo> Versions { get; set; }
}