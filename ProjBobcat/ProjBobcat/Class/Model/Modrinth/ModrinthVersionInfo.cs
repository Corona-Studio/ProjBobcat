using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthDependencyModelComparer : IEqualityComparer<ModrinthDependency>
{
    public bool Equals(ModrinthDependency? x, ModrinthDependency? y)
    {
        if (x == null && y == null) return false;
        if (x == null || y == null) return false;

        return x.ProjectId == y.ProjectId;
    }

    public int GetHashCode(ModrinthDependency obj)
    {
        return (obj.ProjectId ?? string.Empty).GetHashCode();
    }
}

public class ModrinthFileInfo
{
    [JsonPropertyName("hashes")] public IReadOnlyDictionary<string, string>? Hashes { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("filename")] public required string FileName { get; init; }

    [JsonPropertyName("primary")] public bool Primary { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }
}

public class ModrinthDependency
{
    [JsonPropertyName("version_id")] public string? VersionId { get; set; }

    [JsonPropertyName("project_id")] public string? ProjectId { get; set; }

    [JsonPropertyName("dependency_type")] public string? DependencyType { get; set; }
}

public class ModrinthVersionInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("project_id")] public string? ProjectId { get; set; }

    [JsonPropertyName("author_id")] public string? AuthorId { get; set; }

    [JsonPropertyName("featured")] public bool Featured { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("version_number")] public string? VersionNumber { get; set; }

    [JsonPropertyName("changelog")] public string? ChangeLog { get; set; }

    [JsonPropertyName("changelog_url")] public string? ChangeLogUrl { get; set; }

    [JsonPropertyName("date_published")] public DateTime PublishDate { get; set; }

    [JsonPropertyName("downloads")] public int Downloads { get; set; }

    [JsonPropertyName("version_type")] public string? VersionType { get; set; }

    [JsonPropertyName("files")] public ModrinthFileInfo[] Files { get; set; } = [];

    [JsonPropertyName("loaders")] public string[]? Loaders { get; set; }

    [JsonPropertyName("dependencies")] public ModrinthDependency[]? Dependencies { get; set; }

    [JsonPropertyName("game_versions")] public string[] GameVersion { get; set; } = [];
}

[JsonSerializable(typeof(ModrinthVersionInfo))]
[JsonSerializable(typeof(ModrinthVersionInfo[]))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, ModrinthVersionInfo>))]
partial class ModrinthVersionInfoContext : JsonSerializerContext;