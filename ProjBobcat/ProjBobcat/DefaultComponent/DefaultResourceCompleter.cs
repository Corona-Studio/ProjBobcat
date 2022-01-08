using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent;
#nullable enable
/// <summary>
///     默认的资源补全器
/// </summary>
public class DefaultResourceCompleter : IResourceCompleter
{
    static readonly object ResolveEventKey = new();
    static readonly object ChangedEventKey = new();
    static readonly object CompletedEventKey = new();

    readonly ConcurrentBag<DownloadFile> _failedFiles = new();
    readonly EventHandlerList _listEventDelegates = new();

    bool disposedValue;

    public int TotalDownloaded { get; private set; }
    public int NeedToDownload { get; private set; }

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(10);
    public int DownloadParts { get; set; } = 16;
    public int TotalRetry { get; set; }
    public bool CheckFile { get; set; }
    public IEnumerable<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus
    {
        add => _listEventDelegates.AddHandler(ResolveEventKey, value);
        remove => _listEventDelegates.RemoveHandler(ResolveEventKey, value);
    }

    public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent
    {
        add => _listEventDelegates.AddHandler(ChangedEventKey, value);
        remove => _listEventDelegates.RemoveHandler(ChangedEventKey, value);
    }

    public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent
    {
        add => _listEventDelegates.AddHandler(CompletedEventKey, value);
        remove => _listEventDelegates.RemoveHandler(CompletedEventKey, value);
    }

    public TaskResult<ResourceCompleterCheckResult?> CheckAndDownload()
    {
        return CheckAndDownloadTaskAsync().Result;
    }

    public async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync()
    {
        if (!(ResourceInfoResolvers?.Any() ?? false))
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        var totalLostFiles = new List<IGameResource>();
        foreach (var resolver in ResourceInfoResolvers)
        {
            if (_listEventDelegates[ResolveEventKey] is EventHandler<GameResourceInfoResolveEventArgs> handler)
                resolver.GameResourceInfoResolveEvent += handler;

            var lostFiles = await resolver.ResolveResourceAsync();
            totalLostFiles.AddRange(lostFiles);
        }

        if (!totalLostFiles.Any())
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        totalLostFiles.Shuffle();
        NeedToDownload = totalLostFiles.Count;

        var downloadList = new List<DownloadFile>();
        foreach (var f in totalLostFiles)
        {
            var dF = new DownloadFile
            {
                DownloadPath = f.Path,
                DownloadUri = f.Uri,
                FileName = f.FileName,
                FileSize = f.FileSize,
                CheckSum = f.CheckSum,
                FileType = f.Type
            };
            dF.Completed += WhenCompleted;

            downloadList.Add(dF);
        }

        if (downloadList.First().FileType == ResourceType.GameJar)
            downloadList.First().Changed += (_, args) =>
            {
                OnCompleted(downloadList.First(), new DownloadFileCompletedEventArgs(null, null, args.Speed));
            };

        var (item1, item2) = await DownloadFiles(downloadList);

        foreach (var df in downloadList)
        {
            df.Completed -= WhenCompleted;
            df.Dispose();
        }

        return new TaskResult<ResourceCompleterCheckResult?>(item1, value: item2);
    }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~DefaultResourceCompleter()
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

    void OnCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileCompletedEventArgs>) eventList[CompletedEventKey]!;
        @event?.Invoke(sender, e);
    }

    void OnChanged(double progress, double speed)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileChangedEventArgs>) eventList[ChangedEventKey]!;

        @event?.Invoke(this, new DownloadFileChangedEventArgs
        {
            ProgressPercentage = progress,
            Speed = speed
        });
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile df) return;

        if (!(e.Success ?? false)) _failedFiles.Add(df);

        TotalDownloaded++;
        OnChanged((double) TotalDownloaded / NeedToDownload, e.AverageSpeed);
        OnCompleted(sender, e);
    }

    async Task<ValueTuple<TaskResultStatus, ResourceCompleterCheckResult?>> DownloadFiles(
        IEnumerable<DownloadFile> downloadList)
    {
        _failedFiles.Clear();

        await DownloadHelper.AdvancedDownloadListFile(downloadList, new DownloadSettings
        {
            CheckFile = true,
            DownloadParts = DownloadParts,
            HashType = HashType.SHA1,
            RetryCount = TotalRetry,
            Timeout = (int) TimeoutPerFile.TotalMilliseconds
        });

        var isLibraryFailed = _failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = _failedFiles switch
        {
            _ when isLibraryFailed => TaskResultStatus.Error,
            _ when !_failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        return (result, new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = _failedFiles
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) _listEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}