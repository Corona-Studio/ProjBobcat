using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    readonly ConcurrentBag<DownloadFile> _failedFiles = [];

    ulong _needToDownload, _totalDownloaded;

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(10);
    public int DownloadParts { get; set; } = 16;
    public int DownloadThread { get; set; } = 16;
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public int TotalRetry { get; set; } = 2;
    public bool CheckFile { get; set; } = true;
    public IReadOnlyList<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    public event EventHandler<GameResourceInfoResolveEventArgs>? GameResourceInfoResolveStatus;
    public event EventHandler<DownloadFileChangedEventArgs>? DownloadFileChangedEvent;
    public event EventHandler<DownloadFileCompletedEventArgs>? DownloadFileCompletedEvent;

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

        OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = 0,
            Status = "正在进行资源检查"
        });

        var checkAction = new ActionBlock<IResourceInfoResolver>(async resolver =>
        {
            resolver.GameResourceInfoResolveEvent += FireResolveEvent;

            await Parallel.ForEachAsync(
                resolver.ResolveResourceAsync(),
                new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism },
                ReceiveGameResourceTask);

            return;

            void FireResolveEvent(object? sender, GameResourceInfoResolveEventArgs e)
            {
                if (!downloadBag.IsEmpty)
                {
                    resolver.GameResourceInfoResolveEvent -= FireResolveEvent;
                    return;
                }

                OnResolveComplete(sender, e);
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = MaxDegreeOfParallelism,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        });

        foreach(var r in ResourceInfoResolvers!)
            await checkAction.SendAsync(r);

        checkAction.Complete();
        await checkAction.Completion;

        OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = 100,
            Status = "资源检查完成"
        });

        if (downloadBag.IsEmpty)
            return new TaskResult<ResourceCompleterCheckResult?>(
                TaskResultStatus.Success,
                value: new ResourceCompleterCheckResult{FailedFiles = [], IsLibDownloadFailed = false});

        var downloads = downloadBag.ToArray();

        Random.Shared.Shuffle(downloads);

        await DownloadHelper.AdvancedDownloadListFile(downloads, downloadSettings);

        var isLibraryFailed = _failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = _failedFiles switch
        {
            _ when isLibraryFailed => TaskResultStatus.Error,
            _ when !_failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        var resultArgs = new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = _failedFiles
        };

        return new TaskResult<ResourceCompleterCheckResult?>(result, value: resultArgs);

        ValueTask ReceiveGameResourceTask(IGameResource element, CancellationToken ct)
        {
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

            Interlocked.Add(ref _needToDownload, 1);

            return default;
        }
    }

    public void Dispose()
    {
    }

    void OnResolveComplete(object? sender, GameResourceInfoResolveEventArgs e)
    {
        GameResourceInfoResolveStatus?.Invoke(sender, e);
    }

    void OnCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        DownloadFileCompletedEvent?.Invoke(sender, e);
    }

    void OnChanged(double progress, double speed)
    {
        DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
        {
            ProgressPercentage = progress,
            Speed = speed
        });
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile df) return;
        if (!(e.Success ?? false)) _failedFiles.Add(df);

        df.Completed -= WhenCompleted;

        var downloaded = Interlocked.Increment(ref _totalDownloaded);
        
        OnChanged((double)downloaded / _needToDownload, e.AverageSpeed);
        OnCompleted(sender, e);
    }
}