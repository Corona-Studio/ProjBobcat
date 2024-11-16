using System.Collections.Concurrent;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Event;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public abstract class ModPackInstallerBase : InstallerBase
{
    protected readonly ConcurrentBag<DownloadFile> FailedFiles = [];
    protected int TotalDownloaded, NeedToDownload;

    protected void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile file) return;

        this.TotalDownloaded++;

        var progress = (double)this.TotalDownloaded / this.NeedToDownload * 100;
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;
        var fileName = file.FileName.Length > 20
            ? $"{file.FileName[..20]}..."
            : file.FileName;

        this.InvokeStatusChangedEvent(
            $"{retryStr}下载整合包中的 Mods - {fileName} ({this.TotalDownloaded} / {this.NeedToDownload})",
            progress);

        if (!e.Success) this.FailedFiles.Add(file);
    }
}