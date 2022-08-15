using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
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
    public int MaxDegreeOfParallelism { get; set; } = 1;
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

        NeedToDownload = 0;
        _failedFiles.Clear();

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        var gameResourceTransBlock =
            new TransformBlock<IGameResource, DownloadFile>(f =>
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

                    return dF;
                },
                new ExecutionDataflowBlockOptions());
        var downloadFileBlock = new ActionBlock<DownloadFile>(async df =>
        {
            await DownloadHelper.AdvancedDownloadFile(df, new DownloadSettings
            {
                CheckFile = true,
                DownloadParts = DownloadParts,
                HashType = HashType.SHA1,
                RetryCount = TotalRetry,
                Timeout = (int)TimeoutPerFile.TotalMilliseconds
            });

            df.Completed -= WhenCompleted;
            df.Dispose();
        });

        gameResourceTransBlock.LinkTo(downloadFileBlock, linkOptions);

        async Task ReceiveGameResourceTask(IAsyncEnumerable<IGameResource> asyncEnumerable)
        {
            await foreach (var element in asyncEnumerable)
            {
                NeedToDownload++;
                OnResolveComplete(this, new GameResourceInfoResolveEventArgs
                {
                    Progress = 114514,
                    Status = $"发现未下载的 {element.FileName.Max()}({element.Type})，已加入下载队列"
                });
                gameResourceTransBlock.Post(element);
            }
        }

        foreach (var resolver in ResourceInfoResolvers)
        {
            /*
            if (_listEventDelegates[ResolveEventKey] is EventHandler<GameResourceInfoResolveEventArgs> handler)
                resolver.GameResourceInfoResolveEvent += handler;
            */

            if (resolver is VersionInfoResolver or GameLoggingInfoResolver)
            {
                await ReceiveGameResourceTask(resolver.ResolveResourceAsync());
                continue;
            }

            var asyncEnumerable = resolver.ResolveResourceAsync();
            var tasks = new Task[MaxDegreeOfParallelism];
            for (var i = 0; i < tasks.Length; i++)
                tasks[i] = ReceiveGameResourceTask(asyncEnumerable);
            
            await Task.WhenAll(tasks);
        }

        gameResourceTransBlock.Complete();
        await downloadFileBlock.Completion;

        var isLibraryFailed = _failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = _failedFiles switch
        {
            _ when isLibraryFailed => TaskResultStatus.Error,
            _ when !_failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        var resultTuple =  (result, new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = _failedFiles
        });

        return new TaskResult<ResourceCompleterCheckResult?>(resultTuple.Item1, value: resultTuple.Item2);
    }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void OnResolveComplete(object? sender, GameResourceInfoResolveEventArgs e)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<GameResourceInfoResolveEventArgs>)eventList[ResolveEventKey]!;
        @event?.Invoke(sender, e);
    }

    void OnCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileCompletedEventArgs>)eventList[CompletedEventKey]!;
        @event?.Invoke(sender, e);
    }

    void OnChanged(double progress, double speed)
    {
        var eventList = _listEventDelegates;
        var @event = (EventHandler<DownloadFileChangedEventArgs>)eventList[ChangedEventKey]!;

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
        OnChanged((double)TotalDownloaded / NeedToDownload, e.AverageSpeed);
        OnCompleted(sender, e);
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