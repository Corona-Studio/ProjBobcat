using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    /// <summary>
    ///     Download thread count
    /// </summary>
    public static int DownloadThread { get; set; } = 8;

    private const int DefaultCopyBufferSize = 1024 * 8 * 10;

    public static string GetTempDownloadPath()
    {
        var lxTempDir = Path.Combine(Path.GetTempPath(), "LauncherX");

        return lxTempDir;
    }

    public static string GetTempFilePath()
    {
        return Path.Combine(GetTempDownloadPath(), Path.GetRandomFileName());
    }

    private static async Task RecycleDownloadFile(AbstractDownloadBase download)
    {
        // Once we finished the download, we need to dispose the kept file stream
        foreach (var (_, stream) in download.FinishedRangeStreams)
        {
            try
            {
                await stream.DisposeAsync();
            }
            catch (Exception e)
            {
                // Do nothing because we don't care about the exception
                Debug.WriteLine(e);
            }
        }

        download.FinishedRangeStreams.Clear();
    }

    public static string AutoFormatSpeedString(double speedInBytePerSecond)
    {
        var speed = AutoFormatSpeed(speedInBytePerSecond);
        var unit = speed.Unit switch
        {
            SizeUnit.B => "B / s",
            SizeUnit.Kb => "Kb / s",
            SizeUnit.Mb => "Mb / s",
            SizeUnit.Gb => "Gb / s",
            SizeUnit.Tb => "Tb / s",
            _ => "B / s"
        };

        return $"{speed.Speed:F} {unit}";
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
    ///     Advanced file download impl
    /// </summary>
    /// <param name="df"></param>
    /// <param name="downloadSettings"></param>
    private static Task AdvancedDownloadFile(AbstractDownloadBase df, DownloadSettings downloadSettings)
    {
        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        return df.FileSize is >= 1048576 or 0
            ? MultiPartDownloadTaskAsync(df, downloadSettings)
            : DownloadData(df, downloadSettings);
    }

    private static (BufferBlock<AbstractDownloadBase> Input, ActionBlock<AbstractDownloadBase> Execution) BuildAdvancedDownloadTplBlock(DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        var bufferBlock = new BufferBlock<AbstractDownloadBase>(new DataflowBlockOptions { EnsureOrdered = false });
        var actionBlock = new ActionBlock<AbstractDownloadBase>(
            d => AdvancedDownloadFile(d, downloadSettings),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DownloadThread * 10,
                MaxDegreeOfParallelism = DownloadThread,
                EnsureOrdered = false
            });

        bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });
        
        return (bufferBlock, actionBlock);
    }

    /// <summary>
    ///     File download method (Auto detect download method)
    /// </summary>
    /// <param name="fileEnumerable">文件列表</param>
    /// <param name="downloadSettings"></param>
    public static async Task AdvancedDownloadListFile(
        IEnumerable<AbstractDownloadBase> fileEnumerable,
        DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        var blocks = BuildAdvancedDownloadTplBlock(downloadSettings);

        foreach (var downloadFile in fileEnumerable)
            await blocks.Input.SendAsync(downloadFile);

        blocks.Input.Complete();
        await blocks.Execution.Completion;
    }

    public static (BufferBlock<AbstractDownloadBase> Input, ActionBlock<AbstractDownloadBase> Execution)
        AdvancedDownloadListFileActionBlock(DownloadSettings downloadSettings) =>
        BuildAdvancedDownloadTplBlock(downloadSettings);

    #endregion
}