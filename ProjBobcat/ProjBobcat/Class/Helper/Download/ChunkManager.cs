using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

/// <summary>
///     Manages download chunks with smart retry and resume capabilities (thread-safe, lockless)
/// </summary>
internal sealed class ChunkManager(DownloadSettings settings) : IDisposable
{
    private readonly ConcurrentDictionary<DownloadRange, ChunkDownloadState> _chunks = [];
    private readonly ConcurrentDictionary<DownloadRange, int> _failedChunks = [];
    private readonly ConcurrentQueue<DownloadRange> _pendingChunks = [];
    private int _completedCount;
    private long _globalAverageSpeedBits; // Store double as long bits for atomic operations

    private double GlobalAverageSpeed
    {
        get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref this._globalAverageSpeedBits));
        set => Interlocked.Exchange(ref this._globalAverageSpeedBits, BitConverter.DoubleToInt64Bits(value));
    }

    public void Dispose()
    {
        foreach (var chunk in this._chunks.Values) chunk?.Dispose();

        this._chunks.Clear();
        this._pendingChunks.Clear();
        this._failedChunks.Clear();
    }

    /// <summary>
    ///     Initialize chunks for download
    /// </summary>
    public void InitializeChunks(IEnumerable<DownloadRange> ranges)
    {
        foreach (var range in ranges) this._pendingChunks.Enqueue(range);
    }

    /// <summary>
    ///     Try to get next chunk to download (thread-safe, lockless)
    /// </summary>
    public bool TryGetNextChunk(out DownloadRange range, out ChunkDownloadState state)
    {
        while (true)
        {
            if (!this._pendingChunks.TryDequeue(out range))
            {
                range = default;
                state = null!;
                return false;
            }

            state = new ChunkDownloadState(range, this.GlobalAverageSpeed);

            if (this._chunks.TryAdd(range, state)) return true;

            // Range already in _chunks (retry case) — replace the old state
            if (this._chunks.TryRemove(range, out var oldState))
            {
                oldState.Dispose();

                if (this._chunks.TryAdd(range, state)) return true;
            }

            state.Dispose();
        }
    }

    /// <summary>
    ///     Mark chunk as completed
    /// </summary>
    public void CompleteChunk(DownloadRange _, ChunkDownloadState state)
    {
        if (state.IsCompleted)
        {
            Interlocked.Increment(ref this._completedCount);
            this.UpdateGlobalSpeed();
        }
    }

    /// <summary>
    ///     Handle chunk failure - decide whether to retry or split (thread-safe)
    /// </summary>
    public bool HandleChunkFailure(DownloadRange range, ChunkDownloadState state, bool canSplit)
    {
        var failCount = this._failedChunks.AddOrUpdate(range, 1, (_, count) => count + 1);
        state.IncrementRetry();

        // If we have some progress and can split, split the remaining part
        if (canSplit && state.BytesDownloaded > 0 && state.GetRemainingBytes() > DownloadHelper.MinimumChunkSize)
        {
            var remainingRange = state.GetRemainingRange();
            if (remainingRange != null)
            {
                var splitCount = Math.Min(4, settings.DownloadParts);
                var splitRanges = SplitRange(remainingRange.Value, splitCount);
                foreach (var splitRange in splitRanges) this._pendingChunks.Enqueue(splitRange);

                // Mark the downloaded portion as completed, preserving the temp file data
                var downloadedRange = state.GetDownloadedRange();
                var completedState = new ChunkDownloadState(downloadedRange, this.GlobalAverageSpeed);
                completedState.UpdateProgress(state.BytesDownloaded);
                completedState.AdoptTempFile(state);

                this._chunks.TryUpdate(range, completedState, state);

                return true;
            }
        }

        // Retry the chunk if under retry limit
        if (failCount < settings.RetryCount || settings.RetryCount <= 0)
        {
            // Remove old entry so TryGetNextChunk can re-add it
            this._chunks.TryRemove(range, out var oldState);
            oldState?.Dispose();

            this._pendingChunks.Enqueue(range);
            return true;
        }

        // Exceeded retry limit
        return false;
    }

    /// <summary>
    ///     Handle slow chunk - cancel and re-queue for faster retry
    /// </summary>
    public void HandleSlowChunk(DownloadRange range, ChunkDownloadState state)
    {
        if (state.RetryCount < 3) // Allow up to 3 speed-based retries
        {
            state.IncrementRetry();

            // If we have progress, create a smaller chunk for retry
            if (state.BytesDownloaded > 0)
            {
                var remainingRange = state.GetRemainingRange();
                if (remainingRange != null) this._pendingChunks.Enqueue(remainingRange.Value);
            }
            else
            {
                this._pendingChunks.Enqueue(range);
            }
        }
    }

    /// <summary>
    ///     Get all completed chunks in order
    /// </summary>
    public IEnumerable<ChunkDownloadState> GetCompletedChunksInOrder()
    {
        return this._chunks.Values
            .Where(c => c.IsCompleted)
            .OrderBy(c => c.Range.Start);
    }

    /// <summary>
    ///     Get total downloaded bytes across all chunks
    /// </summary>
    public long GetTotalDownloadedBytes()
    {
        return this._chunks.Values.Sum(c => c.BytesDownloaded);
    }

    /// <summary>
    ///     Check if all chunks are completed
    /// </summary>
    public bool AreAllChunksCompleted()
    {
        return this._pendingChunks.IsEmpty && this._chunks.Values.All(c => c.IsCompleted);
    }

    /// <summary>
    ///     Get chunk state for a specific range
    /// </summary>
    public ChunkDownloadState? GetChunkState(DownloadRange range)
    {
        this._chunks.TryGetValue(range, out var state);
        return state;
    }

    /// <summary>
    ///     Update global average speed based on completed chunks (thread-safe)
    /// </summary>
    private void UpdateGlobalSpeed()
    {
        var completedChunks = this._chunks.Values.Where(c => c.IsCompleted).ToList();
        if (completedChunks.Count == 0) return;

        var totalSpeed = completedChunks.Sum(c => c.GetAverageSpeed());
        this.GlobalAverageSpeed = totalSpeed / completedChunks.Count;
    }

    /// <summary>
    ///     Split a range into smaller chunks
    /// </summary>
    private static IEnumerable<DownloadRange> SplitRange(DownloadRange range, int parts)
    {
        var length = range.Length;
        var partSize = length / parts;
        var remainder = length % parts;
        var currentStart = range.Start;

        for (var i = 0; i < parts; i++)
        {
            var currentPartSize = partSize + (i == parts - 1 ? remainder : 0);

            yield return new DownloadRange
            {
                Start = currentStart,
                End = currentStart + currentPartSize - 1
            };

            currentStart += currentPartSize;
        }
    }
}