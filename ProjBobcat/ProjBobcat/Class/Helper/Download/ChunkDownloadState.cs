using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

/// <summary>
///     Represents the state of a download chunk with speed monitoring and retry logic
/// </summary>
internal sealed class ChunkDownloadState : IDisposable
{
    private const double MinAcceptableSpeedRatio = 0.1; // 10% of expected speed
    private const int SlowSpeedCheckIntervalMs = 3000; // Check every 3 seconds

    public DownloadRange Range { get; }
    public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);
    public int RetryCount => Interlocked.CompareExchange(ref _retryCount, 0, 0);
    public bool IsCompleted => BytesDownloaded >= Range.Length;
    public FileStream? TempFileStream { get; private set; }
    public string? TempFilePath { get; private set; }

    private long _bytesDownloaded;
    private int _retryCount;
    private readonly DownloadSpeedCalculator _speedCalculator;
    private long _lastSpeedCheckTimestamp;
    private long _lastSpeedCheckBytes;
    private double _expectedSpeed;

    public ChunkDownloadState(DownloadRange range, double expectedSpeed = 0)
    {
        Range = range;
        _speedCalculator = new DownloadSpeedCalculator();

        var startTimestamp = Stopwatch.GetTimestamp();
        _lastSpeedCheckTimestamp = startTimestamp;
        _expectedSpeed = expectedSpeed;
    }

    /// <summary>
    ///     Update download progress and return current speed (thread-safe)
    /// </summary>
    public double UpdateProgress(long additionalBytes)
    {
        Interlocked.Add(ref _bytesDownloaded, additionalBytes);
        return _speedCalculator.AddSample(additionalBytes);
    }

    /// <summary>
    ///     Check if chunk speed is too slow and should be retried
    /// </summary>
    public bool IsTooSlow()
    {
        if (_expectedSpeed <= 0) return false;

        var now = Stopwatch.GetTimestamp();
        var lastCheck = Interlocked.Read(ref _lastSpeedCheckTimestamp);
        var elapsed = (double)(now - lastCheck) / Stopwatch.Frequency;

        if (elapsed < SlowSpeedCheckIntervalMs / 1000.0) return false;

        var currentBytes = BytesDownloaded;
        var lastBytes = Interlocked.Read(ref _lastSpeedCheckBytes);
        var bytesSinceLastCheck = currentBytes - lastBytes;
        var currentSpeed = elapsed > 0 ? bytesSinceLastCheck / elapsed : 0;

        Interlocked.Exchange(ref _lastSpeedCheckTimestamp, now);
        Interlocked.Exchange(ref _lastSpeedCheckBytes, currentBytes);

        // If speed is less than 10% of expected, consider it too slow
        return currentSpeed < (_expectedSpeed * MinAcceptableSpeedRatio);
    }

    /// <summary>
    ///     Get current download speed
    /// </summary>
    public double GetCurrentSpeed() => _speedCalculator.CurrentSpeed;

    /// <summary>
    ///     Get average speed for this chunk
    /// </summary>
    public double GetAverageSpeed() => _speedCalculator.AverageSpeed;

    /// <summary>
    ///     Increment retry count (thread-safe)
    /// </summary>
    public void IncrementRetry() => Interlocked.Increment(ref _retryCount);

    /// <summary>
    ///     Get remaining bytes to download
    /// </summary>
    public long GetRemainingBytes() => Range.Length - BytesDownloaded;

    /// <summary>
    ///     Get actual downloaded range
    /// </summary>
    public DownloadRange GetDownloadedRange()
    {
        return new DownloadRange
        {
            Start = Range.Start,
            End = Range.Start + BytesDownloaded - 1
        };
    }

    /// <summary>
    ///     Get remaining range to download
    /// </summary>
    public DownloadRange? GetRemainingRange()
    {
        if (IsCompleted) return null;

        return new DownloadRange
        {
            Start = Range.Start + BytesDownloaded,
            End = Range.End
        };
    }

    /// <summary>
    ///     Create temp file for storing chunk data
    /// </summary>
    public void CreateTempFile(string tempPath)
    {
        TempFilePath = tempPath;
        TempFileStream = File.Create(tempPath);
    }

    /// <summary>
    ///     Update expected speed for slow speed detection
    /// </summary>
    public void UpdateExpectedSpeed(double expectedSpeed)
    {
        _expectedSpeed = expectedSpeed;
    }

    public void Dispose()
    {
        TempFileStream?.Dispose();
        TempFileStream = null;

        if (this.TempFilePath == null || !File.Exists(this.TempFilePath)) return;

        try
        {
            File.Delete(this.TempFilePath);
        }
        catch
        {
            // Ignore deletion errors
        }
    }
}