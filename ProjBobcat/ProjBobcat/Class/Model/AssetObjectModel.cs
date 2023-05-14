using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

/// <summary>
///     Asset文件信息类
/// </summary>
public class AssetFileInfo
{
    /// <summary>
    ///     Hash检验码
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    /// <summary>
    ///     文件大小
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
///     Asset Object类
/// </summary>
public class AssetObjectModel
{
    /// <summary>
    ///     Asset Objects集合
    /// </summary>
    [JsonPropertyName("objects")]
    public Dictionary<string, AssetFileInfo> Objects { get; set; }
}

[JsonSerializable(typeof(AssetObjectModel))]
partial class AssetObjectModelContext : JsonSerializerContext
{
}