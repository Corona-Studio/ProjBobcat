using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

/// <summary>
///     Manages download chunks with smart retry and resume capabilities
/// </summary>
internal sealed class ChunkManager(DownloadSettings settings) : IDisposable
{
    private readonly ConcurrentDictionary<DownloadRange, ChunkDownloadState> _chunks = [];
    private readonly ConcurrentQueue<DownloadRange> _pendingChunks = [];
    private readonly ConcurrentDictionary<DownloadRange, int> _failedChunks = [];
    private double _globalAverageSpeed;
    private int _completedCount;

    /// <summary>
    ///     Initialize chunks for download
    /// </summary>
    public void InitializeChunks(IEnumerable<DownloadRange> ranges)
    {
        foreach (var range in ranges)
        {
            _pendingChunks.Enqueue(range);
        }
    }

    /// <summary>
    ///     Try to get next chunk to download
    /// </summary>
    public bool TryGetNextChunk(out DownloadRange range, out ChunkDownloadState state)
    {
        while (true)
        {
            // First, try to get a pending chunk
            if (_pendingChunks.TryDequeue(out range))
            {
                state = new ChunkDownloadState(range, _globalAverageSpeed);

                if (_chunks.TryAdd(range, state)) return true;

                // Race condition, chunk already added
                state.Dispose();
                continue;
            }

            // No more pending chunks
            range = default;
            state = null!;

            return false;
        }
    }

    /// <summary>
    ///     Mark chunk as completed
    /// </summary>
    public void CompleteChunk(DownloadRange _, ChunkDownloadState state)
    {
        if (state.IsCompleted)
        {
            Interlocked.Increment(ref _completedCount);
            UpdateGlobalSpeed();
        }
    }

    /// <summary>
    ///     Handle chunk failure - decide whether to retry or split
    /// </summary>
    public bool HandleChunkFailure(DownloadRange range, ChunkDownloadState state, bool canSplit)
    {
        var failCount = _failedChunks.AddOrUpdate(range, 1, (_, count) => count + 1);
        state.IncrementRetry();

        // If we have some progress and can split, split the remaining part
        if (canSplit && state.BytesDownloaded > 0 && state.GetRemainingBytes() > DownloadHelper.MinimumChunkSize)
        {
            var remainingRange = state.GetRemainingRange();
            if (remainingRange != null)
            {
                // Split remaining into smaller chunks
                var splitRanges = SplitRange(remainingRange.Value, settings.DownloadParts);
                foreach (var splitRange in splitRanges)
                {
                    _pendingChunks.Enqueue(splitRange);
                }

                // Mark the downloaded portion as completed
                if (state.BytesDownloaded > 0)
                {
                    var downloadedRange = state.GetDownloadedRange();
                    var completedState = new ChunkDownloadState(downloadedRange, _globalAverageSpeed);
                    completedState.UpdateProgress(state.BytesDownloaded);

                    // Replace the failed chunk state with completed portion
                    _chunks.TryUpdate(range, completedState, state);
                }

                return true;
            }
        }

        // Retry the chunk if under retry limit
        if (failCount < settings.RetryCount || settings.RetryCount <= 0)
        {
            // Re-queue for retry
            _pendingChunks.Enqueue(range);
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
                if (remainingRange != null)
                {
                    _pendingChunks.Enqueue(remainingRange.Value);
                }
            }
            else
            {
                _pendingChunks.Enqueue(range);
            }
        }
    }

    /// <summary>
    ///     Get all completed chunks in order
    /// </summary>
    public IEnumerable<ChunkDownloadState> GetCompletedChunksInOrder()
    {
        return _chunks.Values
            .Where(c => c.IsCompleted)
            .OrderBy(c => c.Range.Start);
    }

    /// <summary>
    ///     Get total downloaded bytes across all chunks
    /// </summary>
    public long GetTotalDownloadedBytes()
    {
        return _chunks.Values.Sum(c => c.BytesDownloaded);
    }

    /// <summary>
    ///     Check if all chunks are completed
    /// </summary>
    public bool AreAllChunksCompleted()
    {
        return _pendingChunks.IsEmpty &&
               _chunks.Values.All(c => c.IsCompleted);
    }

    /// <summary>
    ///     Get chunk state for a specific range
    /// </summary>
    public ChunkDownloadState? GetChunkState(DownloadRange range)
    {
        _chunks.TryGetValue(range, out var state);
        return state;
    }

    /// <summary>
    ///     Update global average speed based on completed chunks
    /// </summary>
    private void UpdateGlobalSpeed()
    {
        var completedChunks = _chunks.Values.Where(c => c.IsCompleted).ToList();
        if (completedChunks.Count == 0) return;

        var totalSpeed = completedChunks.Sum(c => c.GetAverageSpeed());
        _globalAverageSpeed = totalSpeed / completedChunks.Count;
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

    public void Dispose()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk?.Dispose();
        }

        _chunks.Clear();
        _pendingChunks.Clear();
        _failedChunks.Clear();
    }
}