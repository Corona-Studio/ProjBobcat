using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.Downloading;

internal record UrlInfo(long FileLength, bool CanPartialDownload);

public abstract class AbstractDownloadBase : IDownloadFile
{
    internal ConcurrentDictionary<DownloadRange, object?>? Ranges { get; set; }
    internal UrlInfo? UrlInfo { get; set; }
    internal ConcurrentDictionary<DownloadRange, FileStream> FinishedRangeStreams { get; } = [];

    public int PartialDownloadRetryCount { get; internal set; }

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
    public ResourceType FileType { get; internal init; } = ResourceType.Invalid;

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

    public abstract string GetDownloadUrl();

    internal IEnumerable<FileStream> GetFinishedStreamsInorder()
    {
        ArgumentNullException.ThrowIfNull(this.Ranges);

        foreach (var (downloadRange, _) in this.Ranges.OrderBy(p => p.Key.Start))
        {
            if (!this.FinishedRangeStreams.TryGetValue(downloadRange, out var stream))
                throw new InvalidOperationException("Stream not found.");

            yield return stream;
        }
    }

    internal bool IsDownloadFinished()
    {
        ArgumentNullException.ThrowIfNull(this.Ranges);

        return this.Ranges.All(p => this.FinishedRangeStreams.ContainsKey(p.Key));
    }

    internal IEnumerable<DownloadRange> GetUndoneRanges()
    {
        ArgumentNullException.ThrowIfNull(this.Ranges);

        foreach (var (range, _) in this.Ranges)
        {
            if (this.FinishedRangeStreams.ContainsKey(range))
                continue;

            yield return range;
        }
    }

    internal void OnChanged(double speed, ProgressValue progress, long bytesReceived, long totalBytes)
    {
        this.Changed?.Invoke(this, new DownloadFileChangedEventArgs
        {
            Speed = speed,
            ProgressPercentage = progress,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    internal void OnCompleted(bool success, Exception? ex, double averageSpeed)
    {
        this.Completed?.Invoke(this, new DownloadFileCompletedEventArgs(success, ex, averageSpeed));
    }
}