using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

#region 文件信息（校检、下载）

/// <summary>
///     表示一个文件信息。
/// </summary>
public class FileInfo
{
    [JsonIgnore] public string? Name { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; }

    [JsonPropertyName("sha1")] public string Sha1 { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}

#endregion

#region 下载内容

public class GameDownloadInfo
{
    [JsonPropertyName("client")] public FileInfo Client { get; set; }

    [JsonPropertyName("server")] public FileInfo Server { get; set; }
}

#endregion

#region 资源

public class Asset
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("sha1")] public string Sha1 { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("totalSize")] public long TotalSize { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}

#endregion

#region 参数

public class Arguments
{
    [JsonPropertyName("game")] public JsonElement[] Game { get; set; }

    [JsonPropertyName("jvm")] public JsonElement[] Jvm { get; set; }
}

#endregion

#region 库

public class Extract
{
    [JsonPropertyName("exclude")] public string[] Exclude { get; set; }
}

public class Downloads
{
    [JsonPropertyName("artifact")] public FileInfo? Artifact { get; set; }
    [JsonPropertyName("classifiers")] public Dictionary<string, FileInfo> Classifiers { get; set; }
}

public class Library
{
    [JsonPropertyName("downloads")] public Downloads? Downloads { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("extract")] public Extract Extract { get; set; }

    [JsonPropertyName("natives")] public Dictionary<string, string> Natives { get; set; }

    [JsonPropertyName("rules")] public JvmRules[] Rules { get; set; }

    [JsonPropertyName("checksums")] public string[] CheckSums { get; set; }

    [JsonPropertyName("serverreq")] public bool ServerRequired { get; set; }

    [JsonPropertyName("clientreq")] public bool ClientRequired { get; set; } = true;

    [JsonPropertyName("url")] public string Url { get; set; }
}

#endregion

#region 日志

public class Client
{
    [JsonPropertyName("argument")] public string Argument { get; set; }

    [JsonPropertyName("file")] public FileInfo File { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; }
}

public class Logging
{
    [JsonPropertyName("client")] public Client Client { get; set; }
}

#endregion

/// <summary>
///     版本JSON
///     Version's JSON Data Model
/// </summary>
public class RawVersionModel
{
    /// <summary>
    ///     启动参数（老版本）
    ///     Launch arguments for the older versions
    /// </summary>
    [JsonPropertyName("minecraftArguments")]
    public string MinecraftArguments { get; set; }

    /// <summary>
    ///     启动参数
    ///     Launch arguments
    /// </summary>
    [JsonPropertyName("arguments")]
    public Arguments? Arguments { get; set; }

    /// <summary>
    ///     资源信息
    /// </summary>
    [JsonPropertyName("assetIndex")]
    public Asset? AssetIndex { get; set; }

    /// <summary>
    ///     资源版本
    /// </summary>
    [JsonPropertyName("assets")]
    public string AssetsVersion { get; set; }

    /// <summary>
    ///     游戏下载信息
    /// </summary>
    [JsonPropertyName("downloads")]
    public GameDownloadInfo Downloads { get; set; }

    /// <summary>
    ///     游戏版本
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("javaVersion")] public JavaVersionModel? JavaVersion { get; set; }

    /// <summary>
    ///     继承于...（对于有些游戏版本如forge，其JSON配置会直接从另一个版本中继承，并且在继承基础上进行优先级更高的修改）
    ///     Inherits from...(For some game versions like forge, their JSON configuration directly inherits from another
    ///     version, based on which are be modifications with higher priority.)
    /// </summary>
    [JsonPropertyName("inheritsFrom")]
    public string InheritsFrom { get; set; }

    /// <summary>
    ///     库信息
    /// </summary>
    [JsonPropertyName("libraries")]
    public Library[] Libraries { get; set; }

    /// <summary>
    ///     日志
    /// </summary>
    [JsonPropertyName("logging")]
    public Logging Logging { get; set; }

    /// <summary>
    ///     主类
    /// </summary>
    [JsonPropertyName("mainClass")]
    public string? MainClass { get; set; }

    /// <summary>
    ///     最小启动器版本
    /// </summary>
    [JsonPropertyName("minimumLauncherVersion")]
    public int MinimumLauncherVersion { get; set; }

    /// <summary>
    ///     发布时间
    /// </summary>
    [JsonPropertyName("releaseTime")]
    public DateTime? ReleaseTime { get; set; }

    /// <summary>
    ///     时间
    /// </summary>
    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }

    /// <summary>
    ///     类型
    /// </summary>
    [JsonPropertyName("type")]
    public string BuildType { get; set; }

    /// <summary>
    ///     Jar 参数，LiteLoader 使用
    /// </summary>
    [JsonPropertyName("jar")]
    public string? JarFile { get; set; }
}

[JsonSerializable(typeof(RawVersionModel))]
public partial class RawVersionModelContext : JsonSerializerContext
{
}