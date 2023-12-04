using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        await DownloadHelper.AdvancedDownloadListFile(downloadBag, downloadSettings);

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

        var downloaded = Interlocked.Increment(ref _totalDownloaded);
        
        OnChanged((double)downloaded / _needToDownload, e.AverageSpeed);
        OnCompleted(sender, e);
    }
}