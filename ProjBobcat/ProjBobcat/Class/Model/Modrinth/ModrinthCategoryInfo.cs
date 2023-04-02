using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthCategoryInfo
{
    [JsonPropertyName("icon")] public string Icon { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("project_type")] public string ProjectType { get; set; }
}

[JsonSerializable(typeof(ModrinthCategoryInfo))]
[JsonSerializable(typeof(ModrinthCategoryInfo[]))]
partial class ModrinthCategoryInfoContext : JsonSerializerContext
{
}