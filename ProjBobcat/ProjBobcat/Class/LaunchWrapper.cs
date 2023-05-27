using System;
using System.Diagnostics;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

/// <summary>
///     启动包装类
/// </summary>
public class LaunchWrapper : IDisposable
{
    bool disposedValue;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="authResult">验证结果</param>
    public LaunchWrapper(AuthResultBase authResult)
    {
        AuthResult = authResult;
    }

    /// <summary>
    ///     验证结果
    /// </summary>
    public AuthResultBase AuthResult { get; }

    /// <summary>
    ///     退出码
    /// </summary>
    public int ExitCode { get; private set; }

    /// <summary>
    ///     游戏核心
    /// </summary>
    public IGameCore GameCore { get; init; }

    /// <summary>
    ///     游戏进程
    /// </summary>
    public Process? Process { get; init; }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~LaunchWrapper()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     执行过程
    /// </summary>
    public void Do()
    {
        if (Process == null) return;
        if (Process.ProcessName != "Minecraft.Windows")
        {
            Process.BeginOutputReadLine();
            Process.OutputDataReceived += ProcessOnOutputDataReceived;
            Process.BeginErrorReadLine();
            Process.ErrorDataReceived += ProcessOnErrorDataReceived;
        }
        Process.Exited += ProcessOnExited;
    }

    void DisposeManaged()
    {
        if (Process == null) return;

        Process.OutputDataReceived -= ProcessOnOutputDataReceived;
        Process.ErrorDataReceived -= ProcessOnErrorDataReceived;
        Process.Exited -= ProcessOnExited;
    }

    void ProcessOnExited(object sender, EventArgs e)
    {
        try
        {
            ExitCode = Process?.ExitCode ?? -1;
        }
        catch (InvalidOperationException)
        {
            ExitCode = -1;
        }
    }

    void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        if (GameCore is GameCoreBase coreBase)
            coreBase.OnLogGameData(sender, new GameLogEventArgs
            {
                LogType = GameLogType.Unknown,
                RawContent = e.Data
            });
    }

    void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        var totalPrefix = GameCore.GameLogResolver.ResolveTotalPrefix(e.Data);
        var type = GameCore.GameLogResolver.ResolveLogType(string.IsNullOrEmpty(totalPrefix)
            ? e.Data
            : totalPrefix);

        if (type is GameLogType.ExceptionMessage or GameLogType.StackTrace)
        {
            var exceptionMsg = GameCore.GameLogResolver.ResolveExceptionMsg(e.Data);
            var stackTrace = GameCore.GameLogResolver.ResolveStackTrace(e.Data);

            if (GameCore is GameCoreBase gameCoreBase)
                gameCoreBase.OnLogGameData(sender, new GameLogEventArgs
                {
                    LogType = type,
                    RawContent = e.Data,
                    StackTrace = stackTrace,
                    ExceptionMsg = exceptionMsg
                });

            return;
        }

        var time = GameCore.GameLogResolver.ResolveTime(totalPrefix);
        var source = GameCore.GameLogResolver.ResolveSource(totalPrefix);


        if (GameCore is GameCoreBase coreBase)
            coreBase.OnLogGameData(sender, new GameLogEventArgs
            {
                LogType = type,
                RawContent = e.Data,
                Content = e.Data[(totalPrefix?.Length ?? 0)..],
                Source = source,
                Time = time
            });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) Process?.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}
