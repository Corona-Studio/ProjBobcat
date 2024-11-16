using System;
using System.Diagnostics;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

/// <summary>
///     启动包装类
/// </summary>
/// <remarks>
///     构造函数
/// </remarks>
/// <param name="authResult">验证结果</param>
public class LaunchWrapper(AuthResultBase authResult, LaunchSettings launchSettings) : IDisposable
{
    bool _disposedValue;

    /// <summary>
    ///     验证结果
    /// </summary>
    public AuthResultBase AuthResult { get; } = authResult;

    /// <summary>
    ///     启动设置
    /// </summary>
    public LaunchSettings LaunchSettings { get; } = launchSettings;

    /// <summary>
    ///     退出码
    /// </summary>
    public int ExitCode { get; private set; }

    /// <summary>
    ///     游戏核心
    /// </summary>
    public required IGameCore GameCore { get; init; }

    /// <summary>
    ///     游戏进程
    /// </summary>
    public Process? Process { get; init; }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     执行过程
    /// </summary>
    public void Do()
    {
        if (this.Process == null) return;
        if (this.Process.ProcessName != "Minecraft.Windows")
        {
            this.Process.BeginOutputReadLine();
            this.Process.OutputDataReceived += this.ProcessOnOutputDataReceived;
            this.Process.BeginErrorReadLine();
            this.Process.ErrorDataReceived += this.ProcessOnErrorDataReceived;
        }

        this.Process.Exited += this.ProcessOnExited;
    }

    void DisposeManaged()
    {
        if (this.Process == null) return;

        this.Process.OutputDataReceived -= this.ProcessOnOutputDataReceived;
        this.Process.ErrorDataReceived -= this.ProcessOnErrorDataReceived;
        this.Process.Exited -= this.ProcessOnExited;
    }

    void ProcessOnExited(object? sender, EventArgs e)
    {
        if (this.Process == null) return;

        this.ExitCode = ProcessorHelper.TryGetProcessExitCode(this.Process, out var code) ? code : 0;
    }

    void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        if (this.GameCore is GameCoreBase coreBase)
            coreBase.OnLogGameData(sender, new GameLogEventArgs
            {
                LogType = GameLogType.Unknown,
                RawContent = e.Data
            });
    }

    void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        if (this.GameCore.GameLogResolver == null) return;

        var totalPrefix = this.GameCore.GameLogResolver.ResolveTotalPrefix(e.Data);
        var type = this.GameCore.GameLogResolver.ResolveLogType(string.IsNullOrEmpty(totalPrefix)
            ? e.Data
            : totalPrefix);

        if (type is GameLogType.ExceptionMessage or GameLogType.StackTrace)
        {
            var exceptionMsg = this.GameCore.GameLogResolver.ResolveExceptionMsg(e.Data);
            var stackTrace = this.GameCore.GameLogResolver.ResolveStackTrace(e.Data);

            if (this.GameCore is GameCoreBase gameCoreBase)
                gameCoreBase.OnLogGameData(sender, new GameLogEventArgs
                {
                    LogType = type,
                    RawContent = e.Data,
                    StackTrace = stackTrace,
                    ExceptionMsg = exceptionMsg
                });

            return;
        }

        var time = this.GameCore.GameLogResolver.ResolveTime(totalPrefix);
        var source = this.GameCore.GameLogResolver.ResolveSource(totalPrefix);


        if (this.GameCore is GameCoreBase coreBase)
            coreBase.OnLogGameData(sender, new GameLogEventArgs
            {
                GameId = this.LaunchSettings.Version,
                LogType = type,
                RawContent = e.Data,
                Content = e.Data[(totalPrefix?.Length ?? 0)..],
                Source = source,
                Time = time
            });
    }

    void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing)
            {
                this.DisposeManaged();
                this.Process?.Dispose();
            }

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            this._disposedValue = true;
        }
    }
}