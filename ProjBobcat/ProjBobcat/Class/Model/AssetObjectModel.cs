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
    public required string Hash { get; init; }

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
    public required IReadOnlyDictionary<string, AssetFileInfo> Objects { get; init; }
}

[JsonSerializable(typeof(AssetObjectModel))]
public partial class AssetObjectModelContext : JsonSerializerContext;