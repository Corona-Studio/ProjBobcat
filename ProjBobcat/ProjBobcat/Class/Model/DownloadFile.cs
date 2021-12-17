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

    bool disposedValue;

    protected readonly EventHandlerList ListEventDelegates = new();

    /// <summary>
    ///     下载Uri
    /// </summary>
    public string DownloadUri { get; set; }

    /// <summary>
    ///     下载路径
    /// </summary>
    public string DownloadPath { get; set; }

    /// <summary>
    ///     保存的文件名
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    ///     最大重试计数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     文件类型（仅在Lib/Asset补全时可用）
    /// </summary>
    public string FileType { get; set; }

    /// <summary>
    ///     文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     文件检验码
    /// </summary>
    public string CheckSum { get; set; }

    /// <summary>
    ///     请求源
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    ///     超时（毫秒）
    /// </summary>
    public int TimeOut { get; set; } = 60000;

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~DownloadFile()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

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
        add => ListEventDelegates.AddHandler(CompletedEventKey, value);
        remove => ListEventDelegates.RemoveHandler(CompletedEventKey, value);
    }

    /// <summary>
    ///     下载改变事件
    /// </summary>
    public event EventHandler<DownloadFileChangedEventArgs> Changed
    {
        add => ListEventDelegates.AddHandler(ChangedEventKey, value);
        remove => ListEventDelegates.RemoveHandler(ChangedEventKey, value);
    }

    public void OnChanged(double speed, double progress, long bytesReceived, long totalBytes)
    {
        var eventList = ListEventDelegates;
        var @event = (EventHandler<DownloadFileChangedEventArgs>) eventList[ChangedEventKey];
        @event?.Invoke(this, new DownloadFileChangedEventArgs
        {
            Speed = speed,
            ProgressPercentage = progress,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    public void OnCompleted(bool? success, Exception ex, double averageSpeed)
    {
        var eventList = ListEventDelegates;
        var @event = (EventHandler<DownloadFileCompletedEventArgs>) eventList[CompletedEventKey];
        @event?.Invoke(this, new DownloadFileCompletedEventArgs(success, ex, averageSpeed));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
                // TODO: 释放托管状态(托管对象)
                ListEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}