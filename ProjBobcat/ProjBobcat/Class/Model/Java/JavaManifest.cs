using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Java;

public class JavaPlatformManifest
{
    [JsonPropertyName("java-runtime-alpha")]
    public JavaRuntime[]? JavaRuntimeAlpha { get; set; }

    [JsonPropertyName("java-runtime-beta")]
    public JavaRuntime[]? JavaRuntimeBeta { get; set; }

    [JsonPropertyName("java-runtime-delta")]
    public JavaRuntime[]? JavaRuntimeDelta { get; set; }

    [JsonPropertyName("java-runtime-gamma")]
    public JavaRuntime[]? JavaRuntimeGamma { get; set; }

    [JsonPropertyName("java-runtime-gamma-snapshot")]
    public JavaRuntime[]? JavaRuntimeGammaSnapshot { get; set; }

    [JsonPropertyName("jre-legacy")]
    public JavaRuntime[]? JreLegacy { get; set; }

    [JsonPropertyName("minecraft-java-exe")]
    public JavaRuntime[]? MinecraftJavaExe { get; set; }
}

public class JavaRuntime
{
    [JsonPropertyName("availability")]
    public Availability? Availability { get; set; }

    [JsonPropertyName("manifest")]
    public required Manifest Manifest { get; set; }

    [JsonPropertyName("version")]
    public Version? Version { get; set; }
}

public class Availability
{
    [JsonPropertyName("group")]
    public long Group { get; set; }

    [JsonPropertyName("progress")]
    public long Progress { get; set; }
}

public class Manifest
{
    [JsonPropertyName("sha1")]
    public required string Sha1 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

public class Version
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("released")]
    public DateTimeOffset Released { get; set; }
}

public class JavaDownloadFiles
{
    [JsonPropertyName("files")] 
    public Dictionary<string, JavaFile> Files { get; set; } = [];
}
public class JavaFile
{
    [JsonPropertyName("downloads")]
    public JreDownload? Downloads { get; set; }

    [JsonPropertyName("executable")]
    public bool Executable { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class JreDownload
{
    [JsonPropertyName("lzma")]
    public StorageType? Lzma { get; set; }

    [JsonPropertyName("raw")]
    public StorageType? Raw { get; set; }
}

public class StorageType
{
    [JsonPropertyName("sha1")]
    public required string Sha1 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }
}
