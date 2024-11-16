using System;
using System.Text.Json.Serialization;
using ProjBobcat.Class.Helper;

namespace ProjBobcat.Class.Model;

public class OperatingSystemRules
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("arch")] public string? Arch { get; set; }

    public bool IsAllow()
    {
        if (!string.IsNullOrEmpty(this.Name) &&
            !this.Name.Equals(Constants.OsSymbol, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrEmpty(this.Arch) && this.Arch != SystemInfoHelper.GetSystemArch()) return false;

        if (OperatingSystem.IsWindows())
            if (!string.IsNullOrEmpty(this.Version) &&
                this.Version != $"^{Platforms.Windows.SystemInfoHelper.GetWindowsMajorVersion()}\\.")
                return false;

        return true;
    }
}

/// <summary>
///     Jvm规则类
/// </summary>
public class JvmRules
{
    /// <summary>
    ///     需要执行的操作
    /// </summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>
    ///     使用的操作系统集合
    /// </summary>
    [JsonPropertyName("os")]
    public OperatingSystemRules? OperatingSystem { get; set; }
}

[JsonSerializable(typeof(JvmRules))]
[JsonSerializable(typeof(JvmRules[]))]
partial class JvmRulesContext : JsonSerializerContext;