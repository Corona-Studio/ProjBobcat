using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model;

/// <summary>
///     Jvm规则类
/// </summary>
public class JvmRules
{
    /// <summary>
    ///     需要执行的操作
    /// </summary>
    [JsonProperty("action")]
    public string Action { get; set; }

    /// <summary>
    ///     使用的操作系统集合
    /// </summary>
    [JsonProperty("os")]
    public Dictionary<string, string> OperatingSystem { get; set; }
}