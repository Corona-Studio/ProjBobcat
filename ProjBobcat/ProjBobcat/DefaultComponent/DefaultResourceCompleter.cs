using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
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

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(3);
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
        return this.CheckAndDownloadTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync()
    {
        if ((this.ResourceInfoResolvers?.Count ?? 0) == 0)
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        this.DownloadThread = this.DownloadThread <= 1 ? 16 : this.DownloadThread;

        Interlocked.Exchange(ref this._needToDownload, 0);
        this._failedFiles.Clear();

        var downloadSettings = new DownloadSettings
        {
            CheckFile = this.CheckFile,
            DownloadParts = this.DownloadParts,
            HashType = HashType.SHA1,
            RetryCount = this.TotalRetry,
            Timeout = this.TimeoutPerFile
        };

        this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = 0,
            Status = "正在进行资源检查"
        });

        var numBatches = Math.Min(MaxDegreeOfParallelism, Environment.ProcessorCount);
        var blocks = DownloadHelper.AdvancedDownloadListFileActionBlock(downloadSettings);
        var checkAction = new ActionBlock<IResourceInfoResolver>(async resolver =>
        {
            resolver.GameResourceInfoResolveEvent += FireResolveEvent;

            await Parallel.ForEachAsync(
                resolver.ResolveResourceAsync(),
                new ParallelOptions { MaxDegreeOfParallelism = numBatches },
                ReceiveGameResourceTask);

            return;

            void FireResolveEvent(object? sender, GameResourceInfoResolveEventArgs e)
            {
                if (Interlocked.Read(ref this._needToDownload) != 0)
                {
                    resolver.GameResourceInfoResolveEvent -= FireResolveEvent;
                    return;
                }

                this.OnResolveComplete(sender, e);
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = numBatches,
            MaxDegreeOfParallelism = numBatches
        });

        foreach (var r in this.ResourceInfoResolvers!)
            await checkAction.SendAsync(r);

        checkAction.Complete();
        await checkAction.Completion;

        blocks.Input.Complete();
        await blocks.Execution.Completion;

        this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = 100,
            Status = "资源检查完成"
        });

        var isLibraryFailed = this._failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = this._failedFiles switch
        {
            _ when isLibraryFailed => TaskResultStatus.Error,
            _ when !this._failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        var resultArgs = new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = this._failedFiles
        };

        return new TaskResult<ResourceCompleterCheckResult?>(result, value: resultArgs);

        async ValueTask ReceiveGameResourceTask(IGameResource element, CancellationToken ct)
        {
            /*
            this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
            {
                Progress = -1,
                Status = $"发现未下载的 {element.FileName.CropStr()}({element.Type})，已加入下载队列"
            });
            */
            var dF = new DownloadFile
            {
                DownloadPath = element.Path,
                DownloadUri = element.Url,
                FileName = element.FileName,
                FileSize = element.FileSize,
                CheckSum = element.CheckSum,
                FileType = element.Type
            };
            dF.Completed += this.WhenCompleted;

            await blocks.Input.SendAsync(dF, ct);

            Interlocked.Add(ref this._needToDownload, 1);
        }
    }

    public void Dispose()
    {
    }

    void OnResolveComplete(object? sender, GameResourceInfoResolveEventArgs e)
    {
        this.GameResourceInfoResolveStatus?.Invoke(sender, e);
    }

    void OnCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        this.DownloadFileCompletedEvent?.Invoke(sender, e);
    }

    void OnChanged(double progress, double speed)
    {
        this.DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
        {
            ProgressPercentage = progress,
            Speed = speed
        });
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile df) return;
        if (!e.Success) this._failedFiles.Add(df);

        df.Completed -= this.WhenCompleted;

        var downloaded = Interlocked.Increment(ref this._totalDownloaded);
        var needToDownload = Interlocked.Read(ref this._needToDownload);

        this.OnChanged((double)downloaded / needToDownload, e.AverageSpeed);
        this.OnCompleted(sender, e);
    }
}