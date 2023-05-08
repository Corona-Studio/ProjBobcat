using System.Collections.Concurrent;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public class ModPackInstallerBase : InstallerBase
{
    protected readonly ConcurrentBag<DownloadFile> FailedFiles = new();
    protected int TotalDownloaded, NeedToDownload;

    protected void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile file) return;

        TotalDownloaded++;

        var progress = (double)TotalDownloaded / NeedToDownload * 100;
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;
        var fileName = file.FileName.Length > 20
            ? $"{file.FileName[..20]}..."
            : file.FileName;

        InvokeStatusChangedEvent($"{retryStr}下载整合包中的 Mods - {fileName} ({TotalDownloaded} / {NeedToDownload})",
            progress);

        if (!(e.Success ?? false)) FailedFiles.Add(file);
    }
}