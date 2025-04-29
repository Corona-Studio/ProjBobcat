using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace ProjBobcat.Class.Helper.Download;

/// <summary>
///     使用滑动窗口计算下载速度，基于ConcurrentQueue的线程安全实现
/// </summary>
public class DownloadSpeedCalculator
{
    private readonly object _cleanupLock = new(); // 仅用于队列清理操作
    private readonly int _maxSamples;
    private readonly ConcurrentQueue<(DateTime Time, long Bytes)> _speedSamples = new();
    private readonly TimeSpan _windowDuration;
    private double _currentSpeed;
    private long _totalBytes;

    /// <summary>
    ///     初始化下载速度计算器
    /// </summary>
    /// <param name="windowDuration">窗口时间范围，默认5秒</param>
    /// <param name="maxSamples">最大样本数，默认20个</param>
    public DownloadSpeedCalculator(TimeSpan? windowDuration = null, int maxSamples = 20)
    {
        this._windowDuration = windowDuration ?? TimeSpan.FromSeconds(5);
        this._maxSamples = maxSamples;
    }

    /// <summary>
    ///     获取总接收字节数，线程安全
    /// </summary>
    public long TotalBytes => Interlocked.Read(ref this._totalBytes);

    /// <summary>
    ///     添加数据样本并返回当前下载速度（字节/秒）
    /// </summary>
    /// <param name="bytesReceived">新接收的字节数</param>
    /// <returns>当前下载速度（字节/秒）</returns>
    public double AddSample(long bytesReceived)
    {
        var now = DateTime.UtcNow;

        // 原子增加总字节数
        Interlocked.Add(ref this._totalBytes, bytesReceived);

        // 添加新样本
        this._speedSamples.Enqueue((now, bytesReceived));

        // 移除过期样本 - 这部分仍需要锁定以保证一致性
        // ConcurrentQueue不支持从头部删除元素，我们需要定期清理
        if (this._speedSamples.Count > this._maxSamples)
            lock (this._cleanupLock)
            {
                // 再次检查，因为可能另一个线程已经清理过
                if (this._speedSamples.Count > this._maxSamples) this.CleanupOldSamples(now);
            }

        // 计算当前速度
        this.CalculateCurrentSpeed(now);

        return this._currentSpeed;
    }

    /// <summary>
    ///     清理过期样本
    /// </summary>
    private void CleanupOldSamples(DateTime now)
    {
        while (this._speedSamples.Count > this._maxSamples ||
               (this._speedSamples.TryPeek(out var oldestSample) &&
                now - oldestSample.Time > this._windowDuration))
            // 尝试移除队列头部元素
            if (!this._speedSamples.TryDequeue(out _))
                break; // 如果无法移除，中断循环
    }

    /// <summary>
    ///     计算当前速度
    /// </summary>
    private void CalculateCurrentSpeed(DateTime now)
    {
        // 为了计算速度，我们需要拷贝队列内容以避免在计算过程中被修改
        var samples = this._speedSamples.ToArray();

        if (samples.Length < 2)
        {
            Interlocked.Exchange(ref this._currentSpeed, 0);
            return;
        }

        var oldestTime = samples.Min(s => s.Time);
        var timeSpan = (now - oldestTime).TotalSeconds;

        if (timeSpan <= 0.001)
        {
            Interlocked.Exchange(ref this._currentSpeed, 0);
            return;
        }

        var bytesInWindow = samples.Sum(s => s.Bytes);
        var speed = bytesInWindow / timeSpan;

        // 原子更新当前速度
        Interlocked.Exchange(ref this._currentSpeed, speed);
    }

    /// <summary>
    ///     重置计算器，线程安全
    /// </summary>
    public void Reset()
    {
        // 清空队列
        _speedSamples.Clear();

        // 重置计数器
        Interlocked.Exchange(ref this._totalBytes, 0);
        Interlocked.Exchange(ref this._currentSpeed, 0);
    }
}