using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper.Download;
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
    readonly ConcurrentBag<MultiSourceDownloadFile> _failedFiles = [];

    ulong _needToDownload, _totalDownloaded;
    public required IHttpClientFactory HttpClientFactory { get; init; }

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(8);
    public TimeSpan ResolverTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int DownloadParts { get; set; } = 16;
    public int DownloadThread { get; set; } = 16;
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public int TotalRetry { get; set; } = 2;
    public bool CheckFile { get; set; } = true;
    public bool RandomizeDownloadOrder { get; set; } = true;
    public IReadOnlyList<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    public event EventHandler<GameResourceInfoResolveEventArgs>? GameResourceInfoResolveStatus;
    public event EventHandler<DownloadFileChangedEventArgs>? DownloadFileChangedEvent;
    public event EventHandler<GameResourceDownloadedEventArgs>? DownloadFileCompletedEvent;

    record CheckFileInfo(
        IResourceInfoResolver Resolver,
        string BasePath,
        bool CheckLocalFiles,
        ResolvedGameVersion ResolvedGame,
        CancellationToken CancellationToken);

    public TaskResult<ResourceCompleterCheckResult?> CheckAndDownload(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame)
    {
        return this.CheckAndDownloadTaskAsync(basePath, checkLocalFiles, resolvedGame, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    public Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame)
    {
        return this.CheckAndDownloadTaskAsync(basePath, checkLocalFiles, resolvedGame, CancellationToken.None);
    }

    public async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        CancellationToken cancellationToken)
    {
        if ((this.ResourceInfoResolvers?.Count ?? 0) == 0)
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        cancellationToken.ThrowIfCancellationRequested();

        this.DownloadThread = this.DownloadThread <= 1 ? 16 : this.DownloadThread;

        Interlocked.Exchange(ref this._needToDownload, 0);
        Interlocked.Exchange(ref this._totalDownloaded, 0);
        this._failedFiles.Clear();

        var numBatches = Math.Min(MaxDegreeOfParallelism, Environment.ProcessorCount);
        var downloadSettings = new DownloadSettings
        {
            CheckFile = this.CheckFile,
            DownloadParts = this.DownloadParts,
            DownloadThread = DownloadThread,
            HashType = HashType.SHA1,
            RetryCount = this.TotalRetry,
            Timeout = this.TimeoutPerFile,
            HttpClientFactory = this.HttpClientFactory
        };

        this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = ProgressValue.Start,
            Status = "正在进行资源检查"
        });

        var linkOption = new DataflowLinkOptions { PropagateCompletion = true };

        var downloadBlock = this.RandomizeDownloadOrder
            ? DownloadHelper.BuildRandomizingDownloadTplBlock(downloadSettings)
            : DownloadHelper.BuildAdvancedDownloadTplBlock(downloadSettings);

        var checkBlock = new TransformManyBlock<CheckFileInfo, AbstractDownloadBase>(TransformCheckFiles,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = numBatches,
                EnsureOrdered = false,
                CancellationToken = cancellationToken
            });

        checkBlock.LinkTo(downloadBlock, linkOption);

        foreach (var r in this.ResourceInfoResolvers!)
            checkBlock.Post(new CheckFileInfo(r, basePath, checkLocalFiles, resolvedGame, cancellationToken));

        checkBlock.Complete();

        // Add timeout to prevent infinite wait
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ResolverTimeout);

        try
        {
            await downloadBlock.Completion.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
            {
                Progress = ProgressValue.Start,
                Status = "资源检查超时"
            });

            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Error,
                message: "Resource check timed out",
                value: new ResourceCompleterCheckResult
                {
                    IsLibDownloadFailed = true,
                    FailedFiles = [.. this._failedFiles]
                });
        }

        this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = ProgressValue.Finished,
            Status = "资源检查完成"
        });

        var isLibraryFailed = this._failedFiles.Any(d => d.FileType == ResourceType.LibraryOrNative);
        var result = isLibraryFailed switch
        {
            true => TaskResultStatus.Error,
            _ when !this._failedFiles.IsEmpty => TaskResultStatus.PartialSuccess,
            _ => TaskResultStatus.Success
        };

        var resultArgs = new ResourceCompleterCheckResult
        {
            IsLibDownloadFailed = isLibraryFailed,
            FailedFiles = [.. this._failedFiles]
        };

        return new TaskResult<ResourceCompleterCheckResult?>(result, value: resultArgs);
    }

    async IAsyncEnumerable<AbstractDownloadBase> TransformCheckFiles(CheckFileInfo arg)
    {
        arg.Resolver.GameResourceInfoResolveEvent += FireResolveEvent;

        try
        {
            await foreach (var element in arg.Resolver.ResolveResourceAsync(
                               arg.BasePath,
                               arg.CheckLocalFiles,
                               arg.ResolvedGame,
                               arg.CancellationToken))
            {
                var dF = new MultiSourceDownloadFile
                {
                    DownloadPath = element.Path,
                    DownloadUris = element.Urls,
                    FileName = element.FileName,
                    FileSize = element.FileSize,
                    CheckSum = element.CheckSum,
                    FileType = element.Type
                };
                dF.Completed += this.WhenCompleted;

                Interlocked.Increment(ref this._needToDownload);

                yield return dF;
            }
        }
        finally
        {
            arg.Resolver.GameResourceInfoResolveEvent -= FireResolveEvent;
        }

        yield break;

        void FireResolveEvent(object? sender, GameResourceInfoResolveEventArgs e) => this.OnResolveComplete(sender, e);
    }

    public void Dispose()
    {
    }

    void OnResolveComplete(object? sender, GameResourceInfoResolveEventArgs e)
    {
        this.GameResourceInfoResolveStatus?.Invoke(sender, e);
    }

    void OnCompleted(object? sender, GameResourceDownloadedEventArgs e)
    {
        this.DownloadFileCompletedEvent?.Invoke(sender, e);
    }

    void OnChanged(ProgressValue progress, double speed)
    {
        this.DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
        {
            ProgressPercentage = progress,
            Speed = speed
        });
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not MultiSourceDownloadFile df) return;
        if (!e.Success || e.Error != null)
            this._failedFiles.Add(df);

        df.Completed -= this.WhenCompleted;

        var downloaded = Interlocked.Increment(ref this._totalDownloaded);
        var needToDownload = Interlocked.Read(ref this._needToDownload);

        this.OnChanged(ProgressValue.Create(downloaded, needToDownload), e.AverageSpeed);
        this.OnCompleted(sender, new GameResourceDownloadedEventArgs
        {
            TotalNeedToDownload = needToDownload,
            DownloadEventArgs = e
        });
    }
}