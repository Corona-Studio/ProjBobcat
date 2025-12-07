using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.IO;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    internal const string DefaultDownloadClientName = nameof(DownloadHelper);
    private const int DefaultCopyBufferSize = 1024 * 8 * 10;
    internal const int MinimumChunkSize = 1_000_000; // 1 MB

    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    internal static string DefaultUserAgent => $"ProjBobcat {typeof(DownloadHelper).Assembly.GetName().Version}";

    public static string GetTempDownloadPath()
    {
        var lxTempDir = Path.Combine(Path.GetTempPath(), "LauncherX");

        return lxTempDir;
    }

    public static string GetTempFilePath()
    {
        return Path.Combine(GetTempDownloadPath(), Path.GetRandomFileName());
    }

    internal static int CalculateRetryDelay(int retryCount)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, max 10s
        return (int)Math.Min(1000 * Math.Pow(2, retryCount - 1), 10000);
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

        // Auto choose the unit
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

    #region Download a list of files

    /// <summary>
    ///     Advanced file download impl with retry support
    /// </summary>
    /// <param name="df"></param>
    /// <param name="downloadSettings"></param>
    /// <param name="failureTracker"></param>
    private static async Task AdvancedDownloadFileWithRetry(
        AbstractDownloadBase df,
        DownloadSettings downloadSettings,
        ConcurrentDictionary<AbstractDownloadBase, int> failureTracker)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        var maxRetries = downloadSettings.RetryCount > 0 ? downloadSettings.RetryCount : 3;

        try
        {
            if (df.FileSize is >= MinimumChunkSize or 0)
                await MultiPartDownloadTaskAsync(df, downloadSettings).ConfigureAwait(false);
            else
                await DownloadData(df, downloadSettings).ConfigureAwait(false);

            // Success - remove from failure tracker
            failureTracker.TryRemove(df, out _);
        }
        catch (Exception ex)
        {
            // Increment failure count
            var newAttemptCount = failureTracker.AddOrUpdate(df, 1, (_, count) => count + 1);

            if (newAttemptCount <= maxRetries)
            {
                // Will be retried - don't report as error yet
                df.OnChanged(0, ProgressValue.Create(0, 100), 0, 100);
            }
            else
            {
                // Max retries exceeded - report failure
                df.OnCompleted(false, ex, 0);
                failureTracker.TryRemove(df, out _);
            }
        }
    }

    public static ITargetBlock<AbstractDownloadBase> BuildAdvancedDownloadTplBlock(
        DownloadSettings downloadSettings,
        ConcurrentDictionary<AbstractDownloadBase, int>? failureTracker = null)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        failureTracker ??= new ConcurrentDictionary<AbstractDownloadBase, int>();

        var actionBlock = new ActionBlock<AbstractDownloadBase>(
            async d =>
            {
                await AdvancedDownloadFileWithRetry(d, downloadSettings, failureTracker).ConfigureAwait(false);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = downloadSettings.DownloadThread,
                EnsureOrdered = false,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });

        return actionBlock;
    }

    /// <summary>
    ///     Builds a randomizing TPL block that randomly selects files to download.
    ///     This prevents large files from all being queued at the end.
    /// </summary>
    public static ITargetBlock<AbstractDownloadBase> BuildRandomizingDownloadTplBlock(
        DownloadSettings downloadSettings,
        ConcurrentDictionary<AbstractDownloadBase, int>? failureTracker = null)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        failureTracker ??= new ConcurrentDictionary<AbstractDownloadBase, int>();

        var batchBlock = new BatchBlock<AbstractDownloadBase>(downloadSettings.DownloadThread,
            new GroupingDataflowBlockOptions
            {
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });

        var shuffleBlock = new TransformManyBlock<AbstractDownloadBase[], AbstractDownloadBase>(
            batch =>
            {
                Random.Shared.Shuffle(batch);
                return batch;
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = false,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });

        var downloadBlock = new ActionBlock<AbstractDownloadBase>(
            async d =>
            {
                await AdvancedDownloadFileWithRetry(d, downloadSettings, failureTracker).ConfigureAwait(false);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = downloadSettings.DownloadThread,
                EnsureOrdered = false,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });

        batchBlock.LinkTo(shuffleBlock, new DataflowLinkOptions { PropagateCompletion = true });
        shuffleBlock.LinkTo(downloadBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Return a wrapper that exposes the batch block's input interface
        // but completes when the download block completes
        return new TargetBlockWrapper<AbstractDownloadBase>(batchBlock, downloadBlock);
    }

    /// <summary>
    /// Wrapper class that delegates input operations to one block and completion tracking to another
    /// </summary>
    private sealed class TargetBlockWrapper<T> : ITargetBlock<T>
    {
        private readonly ITargetBlock<T> _inputBlock;
        private readonly IDataflowBlock _completionBlock;

        public TargetBlockWrapper(ITargetBlock<T> inputBlock, IDataflowBlock completionBlock)
        {
            _inputBlock = inputBlock;
            _completionBlock = completionBlock;
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue,
            ISourceBlock<T>? source, bool consumeToAccept)
        {
            return _inputBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void Complete()
        {
            _inputBlock.Complete();
        }

        public void Fault(Exception exception)
        {
            _inputBlock.Fault(exception);
        }

        public Task Completion => _completionBlock.Completion;
    }

    /// <summary>
    ///     File download method with automatic retry for failed files
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(
        IReadOnlyList<AbstractDownloadBase> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        var failureTracker = new ConcurrentDictionary<AbstractDownloadBase, int>();
        var maxRetries = downloadSettings.RetryCount > 0 ? downloadSettings.RetryCount : 3;

        // Retry loop: keep downloading until all files succeed or max retries reached
        for (var retryRound = 0; retryRound <= maxRetries; retryRound++)
        {
            var block = BuildAdvancedDownloadTplBlock(downloadSettings, failureTracker);

            if (retryRound == 0)
            {
                foreach (var downloadFile in fileEnumerable)
                    block.Post(downloadFile);
            }
            else
            {
                var failedFiles = failureTracker.Where(kvp => kvp.Value == retryRound).Select(kvp => kvp.Key).ToList();

                if (failedFiles.Count == 0)
                    break;

                await Task.Delay(CalculateRetryDelay(retryRound));

                foreach (var failedFile in failedFiles)
                    block.Post(failedFile);
            }

            block.Complete();
            await block.Completion;

            if (failureTracker.IsEmpty)
                break;
        }
    }

    #endregion
}