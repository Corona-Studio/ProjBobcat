using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    public const string DefaultDownloadClientName = nameof(DownloadHelper);
    public const string DefaultCurseForgeDownloadClientName = "CurseForgeDownloader";
    internal const int MinimumChunkSize = 1_000_000;

    internal static string DefaultUserAgent => $"ProjBobcat {typeof(DownloadHelper).Assembly.GetName().Version}";

    public static string GetTempDownloadPath()
    {
        return Path.Combine(Path.GetTempPath(), "LauncherX");
    }

    public static string GetTempFilePath()
    {
        var tempPath = GetTempDownloadPath();
        Directory.CreateDirectory(tempPath);
        return Path.Combine(tempPath, Path.GetRandomFileName());
    }

    internal static TimeSpan CalculateRetryDelay(int retryCount)
    {
        // IDM-like short backoff. Mirrors are rotated on every attempt, so long waits only make recovery worse.
        var exponentialMs = Math.Min(200 * Math.Pow(2, Math.Max(0, retryCount - 1)), 3_000);
        return TimeSpan.FromMilliseconds(exponentialMs + Random.Shared.Next(25, 175));
    }

    public static string AutoFormatSpeedString(double speedInBytePerSecond)
    {
        var (speed, sizeUnit) = AutoFormatSpeed(speedInBytePerSecond);
        var unit = sizeUnit switch
        {
            SizeUnit.B => " B / s",
            SizeUnit.Kb => "Kb / s",
            SizeUnit.Mb => "Mb / s",
            SizeUnit.Gb => "Gb / s",
            SizeUnit.Tb => "Tb / s",
            _ => " B / s"
        };

        return $"{speed:F1} {unit,6}";
    }

    public static (double Speed, SizeUnit Unit) AutoFormatSpeed(double transferSpeed)
    {
        const double baseNum = 1024;
        const double mbNum = baseNum * baseNum;
        const double gbNum = baseNum * mbNum;
        const double tbNum = baseNum * gbNum;

        var unit = transferSpeed switch
        {
            >= tbNum => SizeUnit.Tb,
            >= gbNum => SizeUnit.Gb,
            >= mbNum => SizeUnit.Mb,
            >= baseNum => SizeUnit.Kb,
            _ => SizeUnit.B
        };

        var convertedSpeed = unit switch
        {
            SizeUnit.Kb => transferSpeed / baseNum,
            SizeUnit.Mb => transferSpeed / mbNum,
            SizeUnit.Gb => transferSpeed / gbNum,
            SizeUnit.Tb => transferSpeed / tbNum,
            _ => transferSpeed
        };

        return (convertedSpeed, unit);
    }

    /// <summary>
    ///     Downloads one file. Range support, connection count, mirror switching and retries are selected automatically.
    /// </summary>
    public static Task DownloadAsync(
        AbstractDownloadBase downloadFile,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloadFile);
        ArgumentNullException.ThrowIfNull(downloadSettings);

        var requestedParts = downloadSettings.DownloadParts > 0 ? downloadSettings.DownloadParts : DefaultMaxParts;
        var connectionCount = Math.Clamp(Math.Max(downloadSettings.DownloadThread, requestedParts), 1,
            MaxConcurrentConnections);
        var connectionGate = new SemaphoreSlim(connectionCount, connectionCount);
        return DownloadAndDisposeGateAsync(downloadFile, downloadSettings, connectionGate, cancellationToken);
    }

    /// <summary>
    ///     Downloads a list using a shared connection budget. Progress raised by each file is the aggregate, monotonic
    ///     progress of the list, which prevents concurrent files from making a UI progress bar jump backwards.
    /// </summary>
    public static async Task DownloadAsync(
        IReadOnlyList<AbstractDownloadBase> downloadFiles,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloadFiles);
        ArgumentNullException.ThrowIfNull(downloadSettings);
        if (downloadFiles.Count == 0) return;

        var requestedParts = downloadSettings.DownloadParts > 0 ? downloadSettings.DownloadParts : DefaultMaxParts;
        var connectionCount = Math.Clamp(Math.Max(downloadSettings.DownloadThread, requestedParts), 1,
            MaxConcurrentConnections);
        using var connectionGate = new SemaphoreSlim(connectionCount, connectionCount);
        var progress = new AggregateDownloadProgress(downloadFiles, downloadSettings.ProgressInterval);
        var fileConcurrency = Math.Min(downloadFiles.Count, connectionCount);

        await Parallel.ForEachAsync(
                downloadFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = fileConcurrency,
                    CancellationToken = cancellationToken
                },
                async (file, ct) =>
                    await DownloadFileCoreAsync(file, downloadSettings, connectionGate, progress.Report, ct)
                        .ConfigureAwait(false))
            .ConfigureAwait(false);
    }

    static async Task DownloadAndDisposeGateAsync(
        AbstractDownloadBase downloadFile,
        DownloadSettings downloadSettings,
        SemaphoreSlim connectionGate,
        CancellationToken cancellationToken)
    {
        using (connectionGate)
            await DownloadFileCoreAsync(downloadFile, downloadSettings, connectionGate, null, cancellationToken)
                .ConfigureAwait(false);
    }

    sealed class AggregateDownloadProgress
    {
        readonly Dictionary<AbstractDownloadBase, FileProgress> _files;
        readonly long _minimumIntervalTicks;
        readonly object _sync = new();
        long _lastReportTimestamp;

        public AggregateDownloadProgress(IEnumerable<AbstractDownloadBase> files, TimeSpan interval)
        {
            this._files = files.ToDictionary(file => file, file => new FileProgress(file.FileSize));
            this._minimumIntervalTicks = Math.Max(1,
                (long)(Math.Max(0.05, interval.TotalSeconds) * System.Diagnostics.Stopwatch.Frequency));
        }

        public void Report(AbstractDownloadBase file, double speed, long bytesReceived, long totalBytes, bool finished)
        {
            var shouldReport = finished;

            lock (this._sync)
            {
                var state = this._files[file];
                state.Bytes = Math.Max(state.Bytes, bytesReceived);
                state.Total = Math.Max(state.Total, totalBytes);
                state.Speed = finished ? 0 : speed;
                var fraction = finished ? 1 : state.Total > 0 ? Math.Clamp((double)state.Bytes / state.Total, 0, 1) : 0;
                state.Fraction = Math.Max(state.Fraction, fraction);

                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!shouldReport && now - this._lastReportTimestamp >= this._minimumIntervalTicks)
                    shouldReport = true;
                if (!shouldReport) return;

                this._lastReportTimestamp = now;
                var progress = ProgressValue.FromNormalized(this._files.Values.Average(item => item.Fraction));
                var allBytes = this._files.Values.Sum(item => item.Bytes);
                var allTotal = this._files.Values.Sum(item => item.Total);
                var allSpeed = this._files.Values.Sum(item => item.Speed);

                // Keep invocation inside the serialization lock. Concurrent reporters must never deliver an older
                // snapshot after a newer one and make a UI progress bar jump backwards.
                file.OnChanged(allSpeed, progress, allBytes, allTotal);
            }
        }

        sealed class FileProgress(long total)
        {
            public long Bytes { get; set; }
            public long Total { get; set; } = total;
            public double Fraction { get; set; }
            public double Speed { get; set; }
        }
    }
}
