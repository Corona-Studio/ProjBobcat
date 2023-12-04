using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent;

/// <summary>
///     默认的资源补全器
/// </summary>
public class DefaultResourceCompleter : IResourceCompleter
{
    static readonly object ResolveEventKey = new();
    static readonly object ChangedEventKey = new();
    static readonly object CompletedEventKey = new();

    readonly ConcurrentBag<DownloadFile> _failedFiles = [];
    readonly EventHandlerList _listEventDelegates = new();

    bool _disposedValue;

    ulong _needToDownload, _totalDownloaded;

    public ulong TotalDownloaded => Interlocked.Read(ref _totalDownloaded);
    public ulong NeedToDownload => Interlocked.Read(ref _needToDownload);

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(10);
    public int DownloadParts { get; set; } = 16;
    public int DownloadThread { get; set; } = 16;
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public int TotalRetry { get; set; } = 2;
    public bool CheckFile { get; set; } = true;
    public IReadOnlyList<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

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
        if ((ResourceInfoResolvers?.Count ?? 0) == 0)
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        DownloadThread = DownloadThread <= 1 ? 16 : DownloadThread;

        _needToDownload = 0;
        _failedFiles.Clear();

        var downloadBag = new ConcurrentBag<DownloadFile>();
        var downloadSettings = new DownloadSettings
        {
            CheckFile = CheckFile,
            DownloadParts = DownloadParts,
            HashType = HashType.SHA1,
            RetryCount = TotalRetry,
            Timeout = (int)TimeoutPerFile.TotalMilliseconds
        };

        async Task ReceiveGameResourceTask(IAsyncEnumerable<IGameResource> asyncEnumerable)
        {
            var count = 0UL;
            var refreshCounter = 0;

            await foreach (var element in asyncEnumerable)
            {
                count++;

                OnResolveComplete(this, new GameResourceInfoResolveEventArgs
                {
                    Progress = 0,
                    Status = $"发现未下载的 {element.FileName.CropStr()}({element.Type})，已加入下载队列"
                });

                var dF = new DownloadFile
                {
                    DownloadPath = element.Path,
                    DownloadUri = element.Url,
                    FileName = element.FileName,
                    FileSize = element.FileSize,
                    CheckSum = element.CheckSum,
                    FileType = element.Type
                };
                dF.Completed += WhenCompleted;

                downloadBag.Add(dF);

                refreshCounter++;

                if (refreshCounter % 10 == 0)
                {
                    Interlocked.Add(ref _needToDownload, count);
                    count = 0;
                }
            }

            Interlocked.Add(ref _needToDownload, count);
        }

        var chunks = ResourceInfoResolvers!.Chunk(MaxDegreeOfParallelism);

        foreach (var chunk in chunks)
        {
            var tasks = new Task[chunk.Length];
            
            for (var i = 0; i < chunk.Length; i++)
            {
                var resolver = chunk[i];
                var asyncEnumerable = resolver.ResolveResourceAsync();
                tasks[i] = ReceiveGameResourceTask(asyncEnumerable);
            }

            await Task.WhenAll(tasks);
        }

        var arr = downloadBag
            .OrderBy(d => d.FileSize)
            .ToArray();

        /*
        var breakPoint = (int)(arr.Length * (3 / 4d));

        if (breakPoint > 10)
            Random.Shared.Shuffle(arr.AsSpan()[breakPoint..]);
        */

        await DownloadHelper.AdvancedDownloadListFile(arr, downloadSettings);

        var isLibraryFailed = _failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = _failedFiles switch
        {
            _ when isLibraryFailed => TaskResultStatus.Error,
            _ when !_failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        var resultTuple = (result, new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = _failedFiles
        });

        return new TaskResult<ResourceCompleterCheckResult?>(resultTuple.result, value: resultTuple.Item2);
    }

    public void Dispose()
    {
        _listEventDelegates.Dispose();
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

        Interlocked.Increment(ref _totalDownloaded);
        
        OnChanged((double)TotalDownloaded / NeedToDownload, e.AverageSpeed);
        OnCompleted(sender, e);
    }
}