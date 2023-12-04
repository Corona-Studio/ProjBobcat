using System;
using ProjBobcat.Event;

namespace ProjBobcat.Class.Model;

/// <summary>
///     下载文件信息类
/// </summary>
public class DownloadFile
{
    /// <summary>
    ///     下载Uri
    /// </summary>
    public required string DownloadUri { get; init; }

    /// <summary>
    ///     下载路径
    /// </summary>
    public required string DownloadPath { get; init; }

    /// <summary>
    ///     保存的文件名
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///     最大重试计数
    /// </summary>
    public int RetryCount { get; internal set; }

    /// <summary>
    ///     文件类型（仅在Lib/Asset补全时可用）
    /// </summary>
    public ResourceType FileType { get; init; }

    /// <summary>
    ///     文件大小
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    ///     文件检验码
    /// </summary>
    public string? CheckSum { get; init; }

    /// <summary>
    ///     下载完成事件
    /// </summary>
    public event EventHandler<DownloadFileCompletedEventArgs>? Completed;

    /// <summary>
    ///     下载改变事件
    /// </summary>
    public event EventHandler<DownloadFileChangedEventArgs>? Changed;

    public void OnChanged(double speed, double progress, long bytesReceived, long totalBytes)
    {
        Changed?.Invoke(this, new DownloadFileChangedEventArgs
        {
            Speed = speed,
            ProgressPercentage = progress,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    public void OnCompleted(bool? success, Exception? ex, double averageSpeed)
    {
        Completed?.Invoke(this, new DownloadFileCompletedEventArgs(success, ex, averageSpeed));
    }
}