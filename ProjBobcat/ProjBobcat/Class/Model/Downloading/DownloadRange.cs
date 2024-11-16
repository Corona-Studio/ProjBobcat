using System.Diagnostics;

namespace ProjBobcat.Class.Model.Downloading;

/// <summary>
///     下载范围类
/// </summary>
[DebuggerDisplay("[{Start}-{End}]")]
public readonly struct DownloadRange
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
}