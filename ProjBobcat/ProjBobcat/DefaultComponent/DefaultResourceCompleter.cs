using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    readonly SemaphoreSlim _operationLock = new(1, 1);
    readonly object _progressLock = new();

    ulong _needToDownload;
    double _lastReportedProgress;
    public required IHttpClientFactory HttpClientFactory { get; init; }
    public TimeSpan ResolverTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool RandomizeDownloadOrder { get; set; } = true;

    public TimeSpan TimeoutPerFile { get; set; } = TimeSpan.FromSeconds(8);
    public int DownloadParts { get; set; } = 16;
    public int DownloadThread { get; set; } = 16;
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public int TotalRetry { get; set; } = 2;
    public bool CheckFile { get; set; } = true;
    public IReadOnlyList<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    public event EventHandler<GameResourceInfoResolveEventArgs>? GameResourceInfoResolveStatus;
    public event EventHandler<DownloadFileChangedEventArgs>? DownloadFileChangedEvent;
    public event EventHandler<GameResourceDownloadedEventArgs>? DownloadFileCompletedEvent;

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

    public void Dispose()
    {
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
        await this._operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await this.CheckAndDownloadCoreAsync(basePath, checkLocalFiles, resolvedGame, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            this._operationLock.Release();
        }
    }

    async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadCoreAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        CancellationToken cancellationToken)
    {
        var resolvers = this.ResourceInfoResolvers!;

        this.DownloadThread = this.DownloadThread <= 1 ? 16 : this.DownloadThread;

        Interlocked.Exchange(ref this._needToDownload, 0);
        this._lastReportedProgress = 0;
        this._failedFiles.Clear();

        var maxResolverParallelism = Math.Clamp(
            this.MaxDegreeOfParallelism,
            1,
            Math.Min(resolvers.Count, Environment.ProcessorCount));
        var downloadSettings = new DownloadSettings
        {
            CheckFile = this.CheckFile,
            DownloadParts = this.DownloadParts,
            DownloadThread = this.DownloadThread,
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (this.ResolverTimeout != Timeout.InfiniteTimeSpan)
            timeoutCts.CancelAfter(this.ResolverTimeout);

        var operationToken = timeoutCts.Token;

        try
        {
            // Finish discovery before downloading. Mixing random reads for hashing with download writes makes both
            // phases slower, and it causes download totals to change while completion events are already arriving.
            var files = await this.ResolveFilesAsync(
                    resolvers,
                    basePath,
                    checkLocalFiles,
                    resolvedGame,
                    maxResolverParallelism,
                    operationToken)
                .ConfigureAwait(false);

            if (this.RandomizeDownloadOrder)
                Random.Shared.Shuffle(files);

            Interlocked.Exchange(ref this._needToDownload, (ulong)files.Length);

            this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
            {
                Progress = files.Length == 0 ? ProgressValue.Finished : ProgressValue.FromNormalized(0.5),
                Status = files.Length == 0 ? "资源检查完成" : $"资源检查完成，发现 {files.Length} 个文件需要修复"
            });

            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    file.Changed += this.WhenChanged;
                    file.Completed += this.WhenCompleted;
                }

                try
                {
                    await DownloadHelper.DownloadAsync(files, downloadSettings, operationToken).ConfigureAwait(false);
                }
                finally
                {
                    foreach (var file in files)
                    {
                        file.Changed -= this.WhenChanged;
                        file.Completed -= this.WhenCompleted;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
            {
                Progress = ProgressValue.Start,
                Status = "资源检查超时"
            });

            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Error,
                "Resource check timed out",
                new ResourceCompleterCheckResult
                {
                    IsLibDownloadFailed = true,
                    FailedFiles = [.. this._failedFiles]
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
            {
                Progress = ProgressValue.Start,
                Status = $"资源检查失败：{ex.Message}"
            });

            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Error,
                ex.Message,
                new ResourceCompleterCheckResult
                {
                    IsLibDownloadFailed = true,
                    FailedFiles = [.. this._failedFiles]
                });
        }

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

        this.OnResolveComplete(this, new GameResourceInfoResolveEventArgs
        {
            Progress = ProgressValue.Finished,
            Status = Interlocked.Read(ref this._needToDownload) == 0
                ? "资源检查完成"
                : result == TaskResultStatus.Success
                    ? "资源修复完成"
                    : "资源修复完成，但有文件下载失败"
        });

        return new TaskResult<ResourceCompleterCheckResult?>(result, value: resultArgs);
    }

    async Task<MultiSourceDownloadFile[]> ResolveFilesAsync(
        IReadOnlyList<IResourceInfoResolver> resolvers,
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken)
    {
        var pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var files = new ConcurrentDictionary<string, MultiSourceDownloadFile>(pathComparer);
        var resolverProgress = new ConcurrentDictionary<IResourceInfoResolver, double>();
        foreach (var resolver in resolvers)
            resolverProgress.TryAdd(resolver, 0);

        await Parallel.ForEachAsync(resolvers, new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            }, async (resolver, token) =>
            {
                resolver.GameResourceInfoResolveEvent += FireResolveEvent;

                try
                {
                    await foreach (var element in resolver.ResolveResourceAsync(
                                           basePath,
                                           checkLocalFiles,
                                           resolvedGame,
                                           token)
                                       .WithCancellation(token)
                                       .ConfigureAwait(false))
                    {
                        var file = new MultiSourceDownloadFile
                        {
                            DownloadPath = element.Path,
                            DownloadUris = element.Urls,
                            FileName = element.FileName,
                            FileSize = element.FileSize,
                            CheckSum = element.CheckSum,
                            FileType = element.Type
                        };

                        var fullPath = Path.GetFullPath(Path.Combine(file.DownloadPath, file.FileName));
                        files.TryAdd(fullPath, file);
                    }
                }
                finally
                {
                    resolver.GameResourceInfoResolveEvent -= FireResolveEvent;
                }
            }).ConfigureAwait(false);

        return [.. files.Values];

        void FireResolveEvent(object? sender, GameResourceInfoResolveEventArgs e)
        {
            if (e.Progress.NormalizedValue < 0 || sender is not IResourceInfoResolver resolver)
            {
                this.OnResolveComplete(sender, e);
                return;
            }

            lock (this._progressLock)
            {
                resolverProgress.AddOrUpdate(
                    resolver,
                    Math.Clamp(e.Progress.NormalizedValue, 0, 1),
                    (_, current) => Math.Max(current, Math.Clamp(e.Progress.NormalizedValue, 0, 1)));
                var scanProgress = resolverProgress.Values.Average() * 0.5;
                this._lastReportedProgress = Math.Max(this._lastReportedProgress, scanProgress);

                this.OnResolveComplete(sender, new GameResourceInfoResolveEventArgs
                {
                    Progress = ProgressValue.FromNormalized(this._lastReportedProgress),
                    Status = e.Status
                });
            }
        }
    }

    void OnResolveComplete(object? sender, GameResourceInfoResolveEventArgs e)
    {
        this.GameResourceInfoResolveStatus?.Invoke(sender, e);
    }

    void OnCompleted(object? sender, GameResourceDownloadedEventArgs e)
    {
        this.DownloadFileCompletedEvent?.Invoke(sender, e);
    }

    void WhenChanged(object? sender, DownloadFileChangedEventArgs e)
    {
        lock (this._progressLock)
        {
            var progress = 0.5 + Math.Clamp(e.ProgressPercentage.NormalizedValue, 0, 1) * 0.5;
            this._lastReportedProgress = Math.Max(this._lastReportedProgress, progress);

            this.DownloadFileChangedEvent?.Invoke(this, new DownloadFileChangedEventArgs
            {
                ProgressPercentage = ProgressValue.FromNormalized(this._lastReportedProgress),
                Speed = e.Speed,
                BytesReceived = e.BytesReceived,
                TotalBytes = e.TotalBytes
            });
        }
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not MultiSourceDownloadFile df) return;
        if (!e.Success || e.Error != null)
            this._failedFiles.Add(df);

        var needToDownload = Interlocked.Read(ref this._needToDownload);

        this.OnCompleted(sender, new GameResourceDownloadedEventArgs
        {
            TotalNeedToDownload = needToDownload,
            DownloadEventArgs = e
        });
    }
}
