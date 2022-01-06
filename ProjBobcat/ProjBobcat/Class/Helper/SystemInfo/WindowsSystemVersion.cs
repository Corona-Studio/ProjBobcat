using System;

namespace ProjBobcat.Class.Helper.SystemInfo;

/// <summary>
///     表示一个 Windows 系统版本。
/// </summary>
public class WindowsSystemVersion : IFormattable, IEquatable<WindowsSystemVersion>
{
    readonly byte _version;

    public WindowsSystemVersion()
    {
        _version = 0;
    }

    public WindowsSystemVersion(byte version)
    {
        _version = version;
    }

    /// <summary>
    ///     未知。
    /// </summary>
    public static WindowsSystemVersion Unknown { get; } = new();

    /// <summary>
    ///     XP 。
    /// </summary>
    public static WindowsSystemVersion WindowsXP { get; } = new(1);

    /// <summary>
    ///     2003 。
    /// </summary>
    public static WindowsSystemVersion Windows2003 { get; } = new(2);

    /// <summary>
    ///     2008 。
    /// </summary>
    public static WindowsSystemVersion Windows2008 { get; } = new(3);

    /// <summary>
    ///     7 。
    /// </summary>
    public static WindowsSystemVersion Windows7 { get; } = new(4);

    /// <summary>
    ///     8 。
    /// </summary>
    public static WindowsSystemVersion Windows8 { get; } = new(5);

    /// <summary>
    ///     8.1 。
    /// </summary>
    public static WindowsSystemVersion Windows8Dot1 { get; } = new(6);

    /// <summary>
    ///     10 。
    /// </summary>
    public static WindowsSystemVersion Windows10 { get; } = new(7);

    /// <summary>
    ///     获取当前程序运行所在的系统版本。
    /// </summary>
    public static WindowsSystemVersion CurrentVersion
        => (Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor) switch
        {
            "10.0" => Windows10,
            "6.3" => Windows8Dot1,
            "6.2" => Windows8,
            "6.1" => Windows7,
            "6.0" => Windows2008,
            "5.2" => Windows2003,
            "5.1" => WindowsXP,
            _ => Unknown
        };

    /// <summary>
    ///     判断一个 <see cref="WindowsSystemVersion" /> 是否与当前实例相同。
    /// </summary>
    /// <param name="other">要判断的 <see cref="WindowsSystemVersion" /> 。</param>
    /// <returns>判断结果。</returns>
    public bool Equals(WindowsSystemVersion other)
    {
        if(other == null) return false;

        return _version == other._version;
    }

    /// <summary>
    ///     根据指定格式返回一个表示当前实例的字符串。
    /// </summary>
    /// <param name="format">根据 <see cref="String.Format(string, object)" /> 的中第一个参数的格式，其中的 "{0}" 会被替换为对应字符串。</param>
    /// <param name="formatProvider">此参数将被忽略。</param>
    /// <returns>对应的字符串。</returns>
    public string ToString(string format, IFormatProvider formatProvider = null)
    {
        return format == null ? ToString() : string.Format(format, ToString());
    }

    /// <summary>
    ///     判断一个对象是否与当前实例相同。
    /// </summary>
    /// <param name="obj">要判断的对象。</param>
    /// <returns>判断结果。</returns>
    public override bool Equals(object obj)
    {
        if (obj is WindowsSystemVersion systemVersion) return _version == systemVersion._version;
        return false;
    }

    /// <summary>
    ///     获取当前实例的哈希值。
    /// </summary>
    /// <returns>当前实例的哈希值。</returns>
    public override int GetHashCode()
    {
        return _version;
    }

    /// <summary>
    ///     返回一个表示当前实例的字符串。
    ///     这将得到如 "8.1" 、 "8" 、 "2003" 、 "XP" 、 "unknown" 等。
    /// </summary>
    /// <returns>对应的字符串。</returns>
    public override string ToString()
    {
        return _version switch
        {
            7 => "10",
            6 => "8.1",
            5 => "8",
            4 => "7",
            3 => "2008",
            2 => "2003",
            1 => "XP",
            0 => "unknown",
            _ => "unknown"
        };
    }

    /// <summary>
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(WindowsSystemVersion left, WindowsSystemVersion right)
    {
        return left?._version == right?._version;
    }

    /// <summary>
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(WindowsSystemVersion left, WindowsSystemVersion right)
    {
        return left?._version != right?._version;
    }
}