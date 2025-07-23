using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Event;

namespace ProjBobcat.DefaultComponent.Installer.ModPackInstaller;

public abstract class ModPackInstallerBase : InstallerBase
{
    protected readonly ConcurrentBag<AbstractDownloadBase> FailedFiles = [];
    protected int TotalDownloaded, NeedToDownload;

    public Func<IEnumerable<string>, IReadOnlyList<string>>? DownloadUriReplacer { get; init; }

    protected void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not AbstractDownloadBase file) return;
        if (!e.Success) this.FailedFiles.Add(file);

        file.Completed -= this.WhenCompleted;

        this.TotalDownloaded++;

        var progress = ProgressValue.Create(this.TotalDownloaded, this.NeedToDownload);
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;
        var fileName = file.FileName.Length > 20
            ? $"{file.FileName[..20]}..."
            : file.FileName;

        this.InvokeStatusChangedEvent(
            $"{retryStr}下载整合包中的 Mods - {fileName} ({this.TotalDownloaded} / {this.NeedToDownload})",
            progress);
    }
}