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
    private readonly DownloadSpeedCalculator _speedCalculator;

    private long _bytesDownloaded;
    private double _expectedSpeed;
    private long _lastSpeedCheckBytes;
    private long _lastSpeedCheckTimestamp;
    private int _retryCount;

    public ChunkDownloadState(DownloadRange range, double expectedSpeed = 0)
    {
        this.Range = range;
        this._speedCalculator = new DownloadSpeedCalculator();

        var startTimestamp = Stopwatch.GetTimestamp();
        this._lastSpeedCheckTimestamp = startTimestamp;
        this._expectedSpeed = expectedSpeed;
    }

    public DownloadRange Range { get; }
    public long BytesDownloaded => Interlocked.Read(ref this._bytesDownloaded);
    public int RetryCount => Interlocked.CompareExchange(ref this._retryCount, 0, 0);
    public bool IsCompleted => this.BytesDownloaded >= this.Range.Length;
    public FileStream? TempFileStream { get; private set; }
    public string? TempFilePath { get; private set; }

    public void Dispose()
    {
        this.TempFileStream?.Dispose();
        this.TempFileStream = null;

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

    /// <summary>
    ///     Update download progress and return current speed (thread-safe)
    /// </summary>
    public double UpdateProgress(long additionalBytes)
    {
        Interlocked.Add(ref this._bytesDownloaded, additionalBytes);
        return this._speedCalculator.AddSample(additionalBytes);
    }

    /// <summary>
    ///     Check if chunk speed is too slow and should be retried
    /// </summary>
    public bool IsTooSlow()
    {
        if (this._expectedSpeed <= 0) return false;

        var now = Stopwatch.GetTimestamp();
        var lastCheck = Interlocked.Read(ref this._lastSpeedCheckTimestamp);
        var elapsed = (double)(now - lastCheck) / Stopwatch.Frequency;

        if (elapsed < SlowSpeedCheckIntervalMs / 1000.0) return false;

        var currentBytes = this.BytesDownloaded;
        var lastBytes = Interlocked.Read(ref this._lastSpeedCheckBytes);
        var bytesSinceLastCheck = currentBytes - lastBytes;
        var currentSpeed = elapsed > 0 ? bytesSinceLastCheck / elapsed : 0;

        Interlocked.Exchange(ref this._lastSpeedCheckTimestamp, now);
        Interlocked.Exchange(ref this._lastSpeedCheckBytes, currentBytes);

        // If speed is less than 10% of expected, consider it too slow
        return currentSpeed < this._expectedSpeed * MinAcceptableSpeedRatio;
    }

    /// <summary>
    ///     Get current download speed
    /// </summary>
    public double GetCurrentSpeed()
    {
        return this._speedCalculator.CurrentSpeed;
    }

    /// <summary>
    ///     Get average speed for this chunk
    /// </summary>
    public double GetAverageSpeed()
    {
        return this._speedCalculator.AverageSpeed;
    }

    /// <summary>
    ///     Increment retry count (thread-safe)
    /// </summary>
    public void IncrementRetry()
    {
        Interlocked.Increment(ref this._retryCount);
    }

    /// <summary>
    ///     Get remaining bytes to download
    /// </summary>
    public long GetRemainingBytes()
    {
        return this.Range.Length - this.BytesDownloaded;
    }

    /// <summary>
    ///     Get actual downloaded range
    /// </summary>
    public DownloadRange GetDownloadedRange()
    {
        return new DownloadRange
        {
            Start = this.Range.Start,
            End = this.Range.Start + this.BytesDownloaded - 1
        };
    }

    /// <summary>
    ///     Get remaining range to download
    /// </summary>
    public DownloadRange? GetRemainingRange()
    {
        if (this.IsCompleted) return null;

        return new DownloadRange
        {
            Start = this.Range.Start + this.BytesDownloaded,
            End = this.Range.End
        };
    }

    /// <summary>
    ///     Create temp file for storing chunk data
    /// </summary>
    public void CreateTempFile(string tempPath)
    {
        this.TempFilePath = tempPath;
        this.TempFileStream = File.Create(tempPath);
    }

    /// <summary>
    ///     Transfer temp file ownership from another state (used when splitting partially downloaded chunks)
    /// </summary>
    internal void AdoptTempFile(ChunkDownloadState source)
    {
        this.TempFilePath = source.TempFilePath;
        this.TempFileStream = source.TempFileStream;
        source.TempFilePath = null;
        source.TempFileStream = null;
    }

    /// <summary>
    ///     Update expected speed for slow speed detection
    /// </summary>
    public void UpdateExpectedSpeed(double expectedSpeed)
    {
        this._expectedSpeed = expectedSpeed;
    }
}