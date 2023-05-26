using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.DefaultComponent.Launch.GameCore;

/// <summary>
///     提供了UWP版本MineCraft的启动核心
/// </summary>
[SupportedOSPlatform("Windows")]
public class DefaultMineCraftUWPCore : GameCoreBase
{
    public override LaunchResult Launch(LaunchSettings launchSettings)
    {
#if WINDOWS
        if (!ProjBobcat.Platforms.Windows.SystemInfoHelper.IsMinecraftUWPInstalled()) throw new InvalidOperationException();

        using var process = new Process
            { StartInfo = new ProcessStartInfo { UseShellExecute = true, FileName = "minecraft:" } };
        process.Start();

        return default;
#endif

        throw new NotSupportedException();
    }

    [Obsolete("UWP启动核心并不支持异步启动")]
#pragma warning disable CS0809 // 过时成员重写未过时成员
    public override Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
#pragma warning restore CS0809 // 过时成员重写未过时成员
    {
        throw new NotImplementedException();
    }
}
