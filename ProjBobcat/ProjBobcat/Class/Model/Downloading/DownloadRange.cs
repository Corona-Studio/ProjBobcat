using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ProjBobcat.Class.Model.Downloading;

/// <summary>
///     下载范围类
/// </summary>
[DebuggerDisplay("[{Start}-{End}]")]
public readonly struct DownloadRange : IComparable<DownloadRange>, IEquatable<DownloadRange>
{
    /// <summary>
    ///     开始字节
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    ///     结束字节
    /// </summary>
    public required long End { get; init; }

    public long Length => this.End - this.Start + 1;

    public override string ToString()
    {
        return $"{this.Start}-{this.End}";
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Start, this.End);
    }

    public int CompareTo(DownloadRange other)
    {
        var startComparison = this.Start.CompareTo(other.Start);
        if (startComparison != 0) return startComparison;

        var endComparison = this.End.CompareTo(other.End);
        return endComparison;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is DownloadRange other && this.Equals(other);
    }

    public bool Equals(DownloadRange other)
    {
        return this.Start == other.Start && this.End == other.End;
    }
    public static bool operator ==(DownloadRange left, DownloadRange right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DownloadRange left, DownloadRange right)
    {
        return !(left == right);
    }

    public static bool operator <(DownloadRange left, DownloadRange right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(DownloadRange left, DownloadRange right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(DownloadRange left, DownloadRange right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(DownloadRange left, DownloadRange right)
    {
        return left.CompareTo(right) >= 0;
    }
}