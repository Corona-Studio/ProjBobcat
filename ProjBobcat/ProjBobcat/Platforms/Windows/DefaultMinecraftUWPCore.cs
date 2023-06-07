using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.Event;

namespace ProjBobcat.Platforms.Windows;

/// <summary>
///     提供了UWP版本MineCraft的启动核心
/// </summary>
[SupportedOSPlatform("Windows")]
public class DefaultMineCraftUWPCore : GameCoreBase
{
    public override LaunchResult Launch(LaunchSettings? launchSettings = null)
    {
#if WINDOWS
        var prevSpan = new TimeSpan();
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        if (!SystemInfoHelper.IsMinecraftUWPInstalled())
            return new LaunchResult
            {
                ErrorType = LaunchErrorType.OperationFailed,
                Error = new ErrorModel
                {
                    Error = "找不到游戏",
                    ErrorMessage = "没有找到UWP版本的Minecraft"
                }
            };

        var psi = new ProcessStartInfo("minecraft:")
        {
            UseShellExecute = true
        };

        Process.Start(psi);

        var uwpProcess = Process.GetProcessesByName("Minecraft.Windows").FirstOrDefault();

        /*using var process = new Process
            { StartInfo = psi };*/

        var launchWrapper = new LaunchWrapper(null!)
        {
            GameCore = this,
            Process = uwpProcess!
        };
        launchWrapper.Do();

        InvokeLaunchLogThenStart("启动游戏", ref prevSpan, ref stopwatch);

        Task.Run(launchWrapper.Process.WaitForExit)
            .ContinueWith(task =>
            {
                OnGameExit(launchWrapper, new GameExitEventArgs
                {
                    Exception = task.Exception,
                    ExitCode = launchWrapper.ExitCode
                });
            });

        return new LaunchResult
        {
            RunTime = stopwatch.Elapsed,
            GameProcess = uwpProcess,
            LaunchSettings = launchSettings
        };
#endif
        return new LaunchResult
        {
            ErrorType = LaunchErrorType.UnsupportedOperatingSystem,
            Error = new ErrorModel
            {
                Error = "不支持的操作系统",
                ErrorMessage = "此操作系统无法启动UWP版本的Minecraft",
                Cause = "UWP版本的Minecraft不支持此操作系统"
            }
        };
    }

    [Obsolete("UWP启动核心并不支持异步启动")]
#pragma warning disable CS0809 // 过时成员重写未过时成员
    public override Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
#pragma warning restore CS0809 // 过时成员重写未过时成员
    {
        throw new NotImplementedException();
    }

    #region 内部方法 Internal Methods

    /// <summary>
    ///     （内部方法）写入日志，记录时间。
    ///     Write the log and record the time.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="time"></param>
    /// <param name="sw"></param>
    void InvokeLaunchLogThenStart(string item, ref TimeSpan time, ref Stopwatch sw)
    {
        OnLogLaunchData(this, new LaunchLogEventArgs
        {
            Item = item,
            ItemRunTime = sw.Elapsed - time
        });
        time = sw.Elapsed;
        sw.Start();
    }

    #endregion
}
