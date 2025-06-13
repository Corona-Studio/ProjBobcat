using System.Collections.Generic;
using System.IO;
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

    public static string AutoFormatSpeedString(double speedInBytePerSecond)
    {
        var speed = AutoFormatSpeed(speedInBytePerSecond);
        var unit = speed.Unit switch
        {
            SizeUnit.B =>  " B / s",
            SizeUnit.Kb => "Kb / s",
            SizeUnit.Mb => "Mb / s",
            SizeUnit.Gb => "Gb / s",
            SizeUnit.Tb => "Tb / s",
            _ =>           " B / s"
        };

        return $"{speed.Speed:F1} {unit,6}";
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
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        if (!Directory.Exists(df.DownloadPath))
            Directory.CreateDirectory(df.DownloadPath);

        return df.FileSize is >= MinimumChunkSize or 0
            ? MultiPartDownloadTaskAsync(df, downloadSettings)
            : DownloadData(df, downloadSettings);
    }

    public static ActionBlock<AbstractDownloadBase> BuildAdvancedDownloadTplBlock(DownloadSettings downloadSettings)
    {
        var lxTempPath = GetTempDownloadPath();

        if (!Directory.Exists(lxTempPath))
            Directory.CreateDirectory(lxTempPath);

        var actionBlock = new ActionBlock<AbstractDownloadBase>(
            async d => await AdvancedDownloadFile(d, downloadSettings).ConfigureAwait(false),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = downloadSettings.DownloadThread,
                EnsureOrdered = false
            });

        return actionBlock;
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

        var block = BuildAdvancedDownloadTplBlock(downloadSettings);

        foreach (var downloadFile in fileEnumerable)
            await block.SendAsync(downloadFile);

        block.Complete();
        await block.Completion;
    }

    #endregion
}