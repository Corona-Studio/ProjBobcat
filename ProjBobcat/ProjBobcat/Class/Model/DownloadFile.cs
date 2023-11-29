using System;
using System.ComponentModel;
using ProjBobcat.Event;

namespace ProjBobcat.Class.Model;

/// <summary>
///     下载文件信息类
/// </summary>
public class DownloadFile : IDisposable
{
    static readonly object CompletedEventKey = new();
    static readonly object ChangedEventKey = new();

    readonly EventHandlerList _listEventDelegates = new();
    bool _disposedValue;

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

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     下载完成事件
    /// </summary>
    public event EventHandler<DownloadFileCompletedEventArgs> Completed
    {
        add => _listEventDelegates.AddHandler(CompletedEventKey, value);
        remove => _listEventDelegates.RemoveHandler(CompletedEventKey, value);
    }

    /// <summary>
    ///     下载改变事件
    /// </summary>
    public event EventHandler<DownloadFileChangedEventArgs> Changed
    {
        add => _listEventDelegates.AddHandler(ChangedEventKey, value);
        remove => _listEventDelegates.RemoveHandler(ChangedEventKey, value);
    }

    public void OnChanged(double speed, double progress, long bytesReceived, long totalBytes)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileChangedEventArgs>?)eventList[ChangedEventKey];
        
        @event?.Invoke(this, new DownloadFileChangedEventArgs
        {
            Speed = speed,
            ProgressPercentage = progress,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    public void OnCompleted(bool? success, Exception? ex, double averageSpeed)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileCompletedEventArgs>?)eventList[CompletedEventKey];
        @event?.Invoke(this, new DownloadFileCompletedEventArgs(success, ex, averageSpeed));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
                // TODO: 释放托管状态(托管对象)
                _listEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            _disposedValue = true;
        }
    }
}