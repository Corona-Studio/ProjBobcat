using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Forge;

/// <summary>
///     Install节点
/// </summary>
public class Install
{
    [JsonPropertyName("profileName")] public string ProfileName { get; set; }

    [JsonPropertyName("target")] public string Target { get; set; }

    [JsonPropertyName("path")] public string Path { get; set; }

    [JsonPropertyName("version")] public string Version { get; set; }

    [JsonPropertyName("filePath")] public string FilePath { get; set; }

    [JsonPropertyName("welcome")] public string Welcome { get; set; }

    [JsonPropertyName("minecraft")] public string MineCraft { get; set; }

    [JsonPropertyName("mirrorList")] public string MirrorList { get; set; }

    [JsonPropertyName("logo")] public string Logo { get; set; }

    [JsonPropertyName("modList")] public string ModList { get; set; }
}

public class ForgeLibraries
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }

    [JsonPropertyName("checksums")] public string[] CheckSums { get; set; }

    [JsonPropertyName("serverreq")] public bool ServerReq { get; set; }

    [JsonPropertyName("clientreq")] public bool ClientReq { get; set; }
}

public class VersionInfo
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("time")] public string Time { get; set; }

    [JsonPropertyName("releaseTime")] public string ReleaseTime { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; }

    [JsonPropertyName("minecraftArguments")]
    public string MinecraftArguments { get; set; }

    [JsonPropertyName("mainClass")] public string MainClass { get; set; }

    [JsonPropertyName("inheritsFrom")] public string InheritsFrom { get; set; }

    [JsonPropertyName("jar")] public string Jar { get; set; }

    [JsonPropertyName("assets")] public string Assets { get; set; }

    [JsonPropertyName("logging")] public JsonElement Logging { get; set; }

    [JsonPropertyName("libraries")] public ForgeLibraries[] Libraries { get; set; }
}

public class Optional
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("client")] public bool Client { get; set; }

    [JsonPropertyName("server")] public bool Server { get; set; }

    [JsonPropertyName("inject")] public bool Inject { get; set; }

    [JsonPropertyName("desc")] public string Desc { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }

    [JsonPropertyName("artifact")] public string Artifact { get; set; }

    [JsonPropertyName("maven")] public string Maven { get; set; }
}

/// <summary>
///     Forge安装文档
/// </summary>
public class LegacyForgeInstallProfile
{
    [JsonPropertyName("install")] public Install Install { get; set; }

    [JsonPropertyName("versionInfo")] public VersionInfo VersionInfo { get; set; }

    [JsonPropertyName("optionals")] public Optional[] OptionalList { get; set; }
}

[JsonSerializable(typeof(VersionInfo))]
partial class LegacyForgeInstallVersionInfoContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(LegacyForgeInstallProfile))]
partial class LegacyForgeInstallProfileContext : JsonSerializerContext
{
}