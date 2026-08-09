using System;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.Downloading;

public abstract class AbstractDownloadBase : IDownloadFile
{
    int _completionRaised;
    int _retryCount;

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
    public int RetryCount
    {
        get => System.Threading.Volatile.Read(ref this._retryCount);
        internal set => System.Threading.Volatile.Write(ref this._retryCount, value);
    }

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

    internal void OnChanged(double speed, ProgressValue progress, long bytesReceived, long totalBytes)
    {
        var args = new DownloadFileChangedEventArgs
        {
            Speed = speed,
            ProgressPercentage = progress,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        };

        if (this.Changed == null) return;
        foreach (EventHandler<DownloadFileChangedEventArgs> handler in this.Changed.GetInvocationList())
            try
            {
                handler(this, args);
            }
            catch
            {
                // A progress subscriber must not interrupt the transfer or prevent other subscribers from updating.
            }
    }

    internal void OnCompleted(bool success, Exception? ex, double averageSpeed)
    {
        if (System.Threading.Interlocked.Exchange(ref this._completionRaised, 1) != 0) return;
        this.Completed?.Invoke(this, new DownloadFileCompletedEventArgs(success, ex, averageSpeed));
    }

    internal int IncrementRetryCount()
    {
        return System.Threading.Interlocked.Increment(ref this._retryCount);
    }

    internal bool CompletionRaised => System.Threading.Volatile.Read(ref this._completionRaised) != 0;

    internal void ResetDownloadState()
    {
        this.RetryCount = 0;
        System.Threading.Volatile.Write(ref this._completionRaised, 0);
    }
}
