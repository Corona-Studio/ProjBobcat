using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    ConcurrentBag<DownloadFile> _retryFiles;

    bool disposedValue;

    protected EventHandlerList listEventDelegates = new();

    public DefaultResourceCompleter()
    {
        _retryFiles = new ConcurrentBag<DownloadFile>();
    }

    public int TotalDownloaded { get; set; }
    public int NeedToDownload { get; set; }

    public int DownloadParts { get; set; } = 16;
    public int TotalRetry { get; set; }
    public bool CheckFile { get; set; }
    public IEnumerable<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus
    {
        add => listEventDelegates.AddHandler(ResolveEventKey, value);
        remove => listEventDelegates.RemoveHandler(ResolveEventKey, value);
    }

    public event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent
    {
        add => listEventDelegates.AddHandler(ChangedEventKey, value);
        remove => listEventDelegates.RemoveHandler(ChangedEventKey, value);
    }

    public event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent
    {
        add => listEventDelegates.AddHandler(CompletedEventKey, value);
        remove => listEventDelegates.RemoveHandler(CompletedEventKey, value);
    }

    public TaskResult<ResourceCompleterCheckResult?> CheckAndDownload()
    {
        return CheckAndDownloadTaskAsync().Result;
    }

    public async Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync()
    {
        _retryFiles.Clear();

        if (!(ResourceInfoResolvers?.Any() ?? false))
            return new TaskResult<ResourceCompleterCheckResult?>(TaskResultStatus.Success, value: null);

        var totalLostFiles = new List<IGameResource>();
        foreach (var resolver in ResourceInfoResolvers)
        {
            var handler = (EventHandler<GameResourceInfoResolveEventArgs>) listEventDelegates[ResolveEventKey]!;
            if (handler != null)
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
                FileType = f.Type,
                TimeOut = 10000
            };
            dF.Completed += WhenCompleted;

            downloadList.Add(dF);
        }

        if (downloadList.First().FileType.Equals("GameJar", StringComparison.OrdinalIgnoreCase))
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
        var eventList = listEventDelegates;
        var @event = (EventHandler<DownloadFileCompletedEventArgs>) eventList[CompletedEventKey]!;
        @event?.Invoke(sender, e);
    }

    void OnChanged(double progress, double speed)
    {
        var eventList = listEventDelegates;
        var @event = (EventHandler<DownloadFileChangedEventArgs>) eventList[ChangedEventKey]!;

        @event?.Invoke(this, new DownloadFileChangedEventArgs
        {
            ProgressPercentage = progress,
            Speed = speed
        });
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile file) return;

        TotalDownloaded++;
        OnChanged((double) TotalDownloaded / NeedToDownload, e.AverageSpeed);
        OnCompleted(sender, e);

        if (!(e.Success ?? false))
        {
            _retryFiles.Add(file);
            return;
        }

        if (!CheckFile) return;

        Check(file, ref _retryFiles);
    }

    static void Check(DownloadFile file, ref ConcurrentBag<DownloadFile> bag)
    {
        var filePath = Path.Combine(file.DownloadPath, file.FileName);
        if (!File.Exists(filePath)) return;

#pragma warning disable CA5350 // 不要使用弱加密算法
        using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

        try
        {
            var hash = CryptoHelper.ComputeFileHash(filePath, hA);

            if (string.IsNullOrEmpty(file.CheckSum)) return;
            if (hash.Equals(file.CheckSum, StringComparison.OrdinalIgnoreCase)) return;

            bag.Add(file);
            File.Delete(filePath);
        }
        catch (Exception)
        {
        }
    }

    async Task<ValueTuple<TaskResultStatus, ResourceCompleterCheckResult?>> DownloadFiles(
        IEnumerable<DownloadFile> downloadList)
    {
        await DownloadHelper.AdvancedDownloadListFile(downloadList, DownloadParts);

        var leftRetries = TotalRetry;
        var fileBag = new ConcurrentBag<DownloadFile>(_retryFiles);

        while (!fileBag.IsEmpty && leftRetries >= 0)
        {
            _retryFiles.Clear();
            TotalDownloaded = 0;
            NeedToDownload = fileBag.Count;

            var files = fileBag.ToList();
            fileBag.Clear();

            foreach (var file in files)
            {
                file.RetryCount++;
                // file.Completed += WhenCompleted;
            }

            await DownloadHelper.AdvancedDownloadListFile(files);

            fileBag = new ConcurrentBag<DownloadFile>(_retryFiles);
            leftRetries--;
        }

        var isLibraryFailed =
            fileBag.Any(f => f.FileType.Equals("Library/Native", StringComparison.OrdinalIgnoreCase));
        var resultType = fileBag.IsEmpty ? TaskResultStatus.Success : TaskResultStatus.PartialSuccess;
        if (isLibraryFailed) resultType = TaskResultStatus.Error;

        return (resultType, new ResourceCompleterCheckResult {IsLibDownloadFailed = isLibraryFailed});
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) listEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}