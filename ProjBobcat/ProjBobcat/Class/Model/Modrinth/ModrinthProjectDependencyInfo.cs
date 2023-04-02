using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectDependencyInfo
{
    [JsonPropertyName("projects")] public ModrinthProjectInfo[] Projects { get; set; }

    [JsonPropertyName("versions")] public ModrinthVersionInfo[] Versions { get; set; }
}

[JsonSerializable(typeof(ModrinthProjectDependencyInfo))]
partial class ModrinthProjectDependencyInfoContext : JsonSerializerContext
{
}