using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
public class DefaultMineCraftUWPCore : GameCoreBase
{
    public override LaunchResult Launch(LaunchSettings launchSettings)
    {
        var timestamp = Stopwatch.GetTimestamp();

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

        var launchWrapper = new LaunchWrapper(null!, launchSettings)
        {
            GameCore = this,
            Process = uwpProcess!
        };
        launchWrapper.Do();

        this.InvokeLaunchLogThenStart("启动游戏", ref timestamp);

        Task.Run(launchWrapper.Process.WaitForExit)
            .ContinueWith(task =>
            {
                this.OnGameExit(launchWrapper, new GameExitEventArgs
                {
                    Exception = task.Exception,
                    ExitCode = launchWrapper.ExitCode
                });
            });

        return new LaunchResult
        {
            RunTime = Stopwatch.GetElapsedTime(timestamp),
            GameProcess = uwpProcess,
            LaunchSettings = launchSettings
        };
    }

    public override Task<LaunchResult> LaunchTaskAsync(LaunchSettings? settings) => throw new InvalidOperationException();
}