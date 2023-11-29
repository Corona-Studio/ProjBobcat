using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherProfile;

/// <summary>
///     Game Profile类
/// </summary>
public class GameProfileModel
{
    /// <summary>
    ///     名称
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     游戏目录
    /// </summary>
    [JsonPropertyName("gameDir")]
    public string? GameDir { get; set; }

    /// <summary>
    ///     创建时间
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    ///     Java虚拟机路径
    /// </summary>
    [JsonPropertyName("javaDir")]
    public string? JavaDir { get; set; }

    /// <summary>
    ///     游戏窗口分辨率
    /// </summary>
    [JsonPropertyName("resolution")]
    public ResolutionModel? Resolution { get; set; }

    /// <summary>
    ///     游戏图标
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    ///     Java虚拟机启动参数
    /// </summary>
    [JsonPropertyName("javaArgs")]
    public string? JavaArgs { get; set; }

    /// <summary>
    ///     最后一次的版本Id
    /// </summary>
    [JsonPropertyName("lastVersionId")]
    public string? LastVersionId { get; set; }

    /// <summary>
    ///     最后一次启动
    /// </summary>
    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }

    /// <summary>
    ///     版本类型
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}