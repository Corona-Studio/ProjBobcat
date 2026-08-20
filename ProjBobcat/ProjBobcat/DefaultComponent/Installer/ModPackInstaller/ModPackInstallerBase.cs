using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Event;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public abstract class ModPackInstallerBase : InstallerBase
{
    protected readonly ConcurrentDictionary<AbstractDownloadBase, byte> FailedFiles = [];
    protected int TotalDownloaded, NeedToDownload;

    public Func<IEnumerable<string>, IReadOnlyList<string>>? DownloadUriReplacer { get; init; }
    public int RetryCount { get; init; } = 3;

    public abstract Task DownloadModsTaskAsync(CancellationToken cancellationToken = default);
    public abstract Task InstallOverridesTaskAsync(CancellationToken cancellationToken = default);

    protected void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not AbstractDownloadBase file) return;

        if (e.Success)
            this.FailedFiles.TryRemove(file, out _);
        else
            this.FailedFiles.TryAdd(file, 0);

        file.Completed -= this.WhenCompleted;

        this.TotalDownloaded++;

        var progress = ProgressValue.Create(this.TotalDownloaded, this.NeedToDownload);
        var retryStr = file.RetryCount > 0 ? $"[Retry - {file.RetryCount}] " : string.Empty;
        var fileName = file.FileName.Length > 20
            ? $"{file.FileName[..20]}..."
            : file.FileName;

        this.InvokeStatusChangedEvent(
            $"{retryStr}Downloading modpack mods - {fileName} ({this.TotalDownloaded} / {this.NeedToDownload})",
            progress);
    }

    protected void PrepareDownloads(IReadOnlyCollection<AbstractDownloadBase> downloadFiles)
    {
        this.FailedFiles.Clear();
        this.TotalDownloaded = 0;
        this.NeedToDownload = downloadFiles.Count;

        foreach (var downloadFile in downloadFiles)
            downloadFile.Completed += this.WhenCompleted;
    }

    protected int GetDownloadRetryCount(IEnumerable<MultiSourceDownloadFile> downloadFiles)
    {
        var sourceCount = downloadFiles
            .Select(file => file.DownloadUris.Count)
            .DefaultIfEmpty(1)
            .Max();

        return Math.Max(1, Math.Max(this.RetryCount, sourceCount));
    }

    protected async Task DownloadFilesTaskAsync(
        IReadOnlyList<AbstractDownloadBase> downloadFiles,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken)
    {
        this.PrepareDownloads(downloadFiles);

        if (downloadFiles.Count == 0) return;

        await DownloadHelper.DownloadAsync(downloadFiles, downloadSettings, cancellationToken).ConfigureAwait(false);

        var failedFiles = this.FailedFiles.Keys.ToArray();
        if (failedFiles.Length == 0) return;

        this.InvokeStatusChangedEvent(
            $"Automatically retrying failed mod downloads ({failedFiles.Length})",
            ProgressValue.Start);

        await Task.Delay(DownloadHelper.CalculateRetryDelay(1), cancellationToken).ConfigureAwait(false);

        this.PrepareDownloads(failedFiles);
        await DownloadHelper.DownloadAsync(failedFiles, downloadSettings, cancellationToken).ConfigureAwait(false);
    }

    protected void ThrowIfDownloadsFailed()
    {
        if (this.FailedFiles.IsEmpty) return;

        var failedFileExceptions = this.FailedFiles.Keys.Select(failedFile =>
        {
            var urls = failedFile switch
            {
                SimpleDownloadFile simple => [simple.DownloadUri],
                MultiSourceDownloadFile multi => multi.DownloadUris.Select(uri => uri.DownloadUri).ToArray(),
                _ => []
            };

            return new Exception($"""
                                 File name: {failedFile.FileName}
                                 Download URLs: [{string.Join(',', urls)}]
                                 Retry count: {failedFile.RetryCount}
                                 """);
        });

        throw new AggregateException(
            "Some modpack files still failed to download after automatic retries. Check the network connection and try again.",
            failedFileExceptions);
    }
}
