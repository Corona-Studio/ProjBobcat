using System;
using System.Diagnostics;

namespace ProjBobcat.Class.Model.Downloading;

/// <summary>
///     下载范围类
/// </summary>
[DebuggerDisplay("[{Start}-{End}]")]
public readonly struct DownloadRange : IComparable<DownloadRange>
{
    /// <summary>
    ///     开始字节
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    ///     结束字节
    /// </summary>
    public required long End { get; init; }

    /// <summary>
    ///     临时文件名称
    /// </summary>
    public required string TempFileName { get; init; }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Start, this.End, this.TempFileName);
    }

    public int CompareTo(DownloadRange other)
    {
        var startComparison = this.Start.CompareTo(other.Start);
        if (startComparison != 0) return startComparison;

        var endComparison = this.End.CompareTo(other.End);
        if (endComparison != 0) return endComparison;

        return string.Compare(this.TempFileName, other.TempFileName, StringComparison.Ordinal);
    }
}