using System;

namespace ProjBobcat.Class.Helper.SystemInfo;

/// <summary>
///     表示一个系统架构。
/// </summary>
public class SystemArch : IFormattable, IEquatable<SystemArch>
{
    bool _is64BitOperatingSystem;

    SystemArch()
    {
    }

    /// <summary>
    ///     X64
    /// </summary>
    public static SystemArch X64 { get; } = new() { _is64BitOperatingSystem = true };

    /// <summary>
    ///     X86
    /// </summary>
    public static SystemArch X86 { get; } = new() { _is64BitOperatingSystem = false };

    /// <summary>
    ///     获取当前程序运行所在的系统架构。
    /// </summary>
    public static SystemArch CurrentArch
        => Environment.Is64BitOperatingSystem ? X64 : X86;

    /// <summary>
    ///     判断一个 <see cref="SystemArch" /> 是否与当前实例相同。
    /// </summary>
    /// <param name="other">要判断的 <see cref="SystemArch" /> 。</param>
    /// <returns>判断结果。</returns>
    public bool Equals(SystemArch other)
    {
        return _is64BitOperatingSystem == other._is64BitOperatingSystem;
    }

    /// <summary>
    ///     根据指定格式返回一个表示当前实例的字符串。
    /// </summary>
    /// <param name="format">根据 <see cref="String.Format(string, object)" /> 的中第一个参数的格式，其中的 "{0}" 会被替换为 "64" 或者 "86"。</param>
    /// <param name="formatProvider">此参数将被忽略。</param>
    /// <returns>对应的字符串。</returns>
    public string ToString(string format, IFormatProvider formatProvider = null)
    {
        return string.IsNullOrEmpty(format)
            ? _is64BitOperatingSystem ? "64" : "86"
            : string.Format(format, _is64BitOperatingSystem ? "64" : "86");
    }

    /// <summary>
    ///     判断一个对象是否与当前实例相同。
    /// </summary>
    /// <param name="obj">要判断的对象。</param>
    /// <returns>判断结果。</returns>
    public override bool Equals(object obj)
    {
        if (obj is SystemArch arch) return _is64BitOperatingSystem == arch._is64BitOperatingSystem;
        return false;
    }

    /// <summary>
    ///     获取当前实例的哈希值。
    /// </summary>
    /// <returns>当前实例的哈希值。</returns>
    public override int GetHashCode()
    {
        return _is64BitOperatingSystem ? 0 : 1;
    }

    /// <summary>
    ///     返回一个表示当前实例的字符串。
    ///     这将得到 "x64" 或 "x86"。
    /// </summary>
    /// <returns>对应的字符串。</returns>
    public override string ToString()
    {
        return _is64BitOperatingSystem ? "x64" : "x86";
    }

    /// <summary>
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(SystemArch left, SystemArch right)
    {
        return left?._is64BitOperatingSystem == right?._is64BitOperatingSystem;
    }

    /// <summary>
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(SystemArch left, SystemArch right)
    {
        return left?._is64BitOperatingSystem != right?._is64BitOperatingSystem;
    }
}