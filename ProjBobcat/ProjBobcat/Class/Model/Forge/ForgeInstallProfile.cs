using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Forge;

public class ForgeInstallProfileData
{
    [JsonPropertyName("client")] public string? Client { get; set; }

    [JsonPropertyName("server")] public string? Server { get; set; }
}

public class ForgeInstallProfileProcessor
{
    [JsonPropertyName("sides")] public string[]? Sides { get; set; }

    [JsonPropertyName("jar")] public required string Jar { get; init; }

    [JsonPropertyName("classpath")] public required string[] ClassPath { get; init; }

    [JsonPropertyName("args")] public required string[] Arguments { get; init; }

    [JsonPropertyName("outputs")] public IReadOnlyDictionary<string, string>? Outputs { get; set; }
}

public class ForgeInstallProfile
{
    [JsonPropertyName("_comment_")] public string[]? Comments { get; set; }

    [JsonPropertyName("spec")] public int Spec { get; set; }

    [JsonPropertyName("profile")] public string? Profile { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("icon")] public string? Icon { get; set; }

    [JsonPropertyName("json")] public string? Json { get; set; }

    [JsonPropertyName("path")] public string? Path { get; set; }

    [JsonPropertyName("logo")] public string? Logo { get; set; }

    [JsonPropertyName("minecraft")] public string? MineCraft { get; set; }

    [JsonPropertyName("welcome")] public string? Welcome { get; set; }

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, ForgeInstallProfileData> Data { get; set; } =
        ImmutableDictionary<string, ForgeInstallProfileData>.Empty;

    [JsonPropertyName("processors")] public ForgeInstallProfileProcessor[] Processors { get; set; } = [];

    [JsonPropertyName("libraries")] public Library[] Libraries { get; set; } = [];
}

[JsonSerializable(typeof(ForgeInstallProfile))]
partial class ForgeInstallProfileContext : JsonSerializerContext;