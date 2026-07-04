using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProjBobcat.Class.Helper.Download;

/// <summary>
///     High-performance download speed calculator using exponential moving average
///     Thread-safe and lock-free implementation
/// </summary>
public class DownloadSpeedCalculator
{
    private const double SmoothingFactor = 0.3; // EMA smoothing factor (0-1)
    private const long MinUpdateIntervalTicks = 500_000; // 50ms in ticks (100ns units)
    private double _currentSpeed;
    private long _lastSampleBytes;
    private long _lastUpdateTicks;
    private long _startTicks;

    private long _totalBytes;

    /// <summary>
    ///     Initialize download speed calculator
    /// </summary>
    public DownloadSpeedCalculator()
    {
        this.Reset();
    }

    /// <summary>
    ///     Get total received bytes (thread-safe)
    /// </summary>
    public long TotalBytes => Interlocked.Read(ref this._totalBytes);

    /// <summary>
    ///     Get current speed in bytes/second (thread-safe)
    /// </summary>
    public double CurrentSpeed => Interlocked.CompareExchange(ref this._currentSpeed, 0, 0);

    /// <summary>
    ///     Get average speed since start
    /// </summary>
    public double AverageSpeed
    {
        get
        {
            var elapsed = this.GetElapsedSeconds();
            return elapsed > 0 ? this.TotalBytes / elapsed : 0;
        }
    }

    /// <summary>
    ///     Add data sample and return current download speed (bytes/second)
    ///     Uses Exponential Moving Average for smooth speed calculation
    /// </summary>
    /// <param name="bytesReceived">Bytes received in this sample</param>
    /// <returns>Current download speed (bytes/second)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double AddSample(long bytesReceived)
    {
        if (bytesReceived <= 0) return this.CurrentSpeed;

        var now = Stopwatch.GetTimestamp();

        // Update total bytes atomically
        Interlocked.Add(ref this._totalBytes, bytesReceived);
        Interlocked.Add(ref this._lastSampleBytes, bytesReceived);

        // Throttle speed calculation updates
        var lastUpdate = Interlocked.Read(ref this._lastUpdateTicks);
        var ticksSinceUpdate = now - lastUpdate;

        if (ticksSinceUpdate < MinUpdateIntervalTicks)
            return this.CurrentSpeed;

        // Try to acquire update lock via CAS
        if (Interlocked.CompareExchange(ref this._lastUpdateTicks, now, lastUpdate) != lastUpdate)
            return this.CurrentSpeed;

        // Calculate instantaneous speed
        var sampleBytes = Interlocked.Exchange(ref this._lastSampleBytes, 0);
        var elapsedSeconds = (double)ticksSinceUpdate / Stopwatch.Frequency;

        if (elapsedSeconds <= 0.001) return this.CurrentSpeed;

        var instantSpeed = sampleBytes / elapsedSeconds;

        // Apply EMA smoothing
        var oldSpeed = Interlocked.CompareExchange(ref this._currentSpeed, 0, 0);
        var newSpeed = oldSpeed == 0
            ? instantSpeed
            : SmoothingFactor * instantSpeed + (1 - SmoothingFactor) * oldSpeed;

        Interlocked.Exchange(ref this._currentSpeed, newSpeed);

        return newSpeed;
    }

    /// <summary>
    ///     Force recalculate speed (useful for getting final accurate speed)
    /// </summary>
    public double Recalculate()
    {
        var elapsed = this.GetElapsedSeconds();
        if (elapsed <= 0) return 0;

        var avgSpeed = this.TotalBytes / elapsed;
        Interlocked.Exchange(ref this._currentSpeed, avgSpeed);
        return avgSpeed;
    }

    /// <summary>
    ///     Reset calculator (thread-safe)
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref this._totalBytes, 0);
        Interlocked.Exchange(ref this._currentSpeed, 0);
        Interlocked.Exchange(ref this._lastSampleBytes, 0);
        var now = Stopwatch.GetTimestamp();
        Interlocked.Exchange(ref this._startTicks, now);
        Interlocked.Exchange(ref this._lastUpdateTicks, now);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetElapsedSeconds()
    {
        var start = Interlocked.Read(ref this._startTicks);
        var elapsed = Stopwatch.GetTimestamp() - start;
        return (double)elapsed / Stopwatch.Frequency;
    }
}