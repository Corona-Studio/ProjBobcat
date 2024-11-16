using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch.GameCore;

public abstract class GameCoreBase : IGameCore
{
    static readonly object GameExitEventKey = new();
    static readonly object GameLogEventKey = new();
    static readonly object LaunchLogEventKey = new();

    protected readonly EventHandlerList ListEventDelegates = new();
    bool _disposedValue;
    public virtual required string RootPath { get; init; }
    public virtual required Guid ClientToken { get; init; }
    public virtual required VersionLocatorBase VersionLocator { get; init; }
    public virtual IGameLogResolver? GameLogResolver { get; init; }

    public event EventHandler<GameExitEventArgs> GameExitEventDelegate
    {
        add => this.ListEventDelegates.AddHandler(GameExitEventKey, value);
        remove => this.ListEventDelegates.RemoveHandler(GameExitEventKey, value);
    }

    public event EventHandler<GameLogEventArgs> GameLogEventDelegate
    {
        add => this.ListEventDelegates.AddHandler(GameLogEventKey, value);
        remove => this.ListEventDelegates.RemoveHandler(GameLogEventKey, value);
    }

    public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate
    {
        add => this.ListEventDelegates.AddHandler(LaunchLogEventKey, value);
        remove => this.ListEventDelegates.RemoveHandler(LaunchLogEventKey, value);
    }

    /// <summary>
    ///     启动 （同步方法）
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public virtual LaunchResult Launch(LaunchSettings settings)
    {
        return this.LaunchTaskAsync(settings).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     启动 （异步方法）
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public abstract Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings);

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~GameCoreBase()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void OnGameExit(object sender, GameExitEventArgs e)
    {
        var eventList = this.ListEventDelegates;
        var @event = (EventHandler<GameExitEventArgs>)eventList[GameExitEventKey]!;
        @event?.Invoke(sender, e);
    }

    public virtual void OnLogGameData(object sender, GameLogEventArgs e)
    {
        var eventList = this.ListEventDelegates;
        var @event = (EventHandler<GameLogEventArgs>)eventList[GameLogEventKey]!;
        @event?.Invoke(sender, e);
    }

    public virtual void OnLogLaunchData(object sender, LaunchLogEventArgs e)
    {
        var eventList = this.ListEventDelegates;
        var @event = (EventHandler<LaunchLogEventArgs>)eventList[LaunchLogEventKey]!;
        @event?.Invoke(sender, e);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing) this.ListEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            this._disposedValue = true;
        }
    }

    /// <summary>
    ///     （内部方法）写入日志，记录时间。
    ///     Write the log and record the time.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="timestamp"></param>
    protected void InvokeLaunchLogThenStart(string item, ref long timestamp)
    {
        this.OnLogLaunchData(this, new LaunchLogEventArgs
        {
            Item = item,
            ItemRunTime = Stopwatch.GetElapsedTime(timestamp)
        });

        timestamp = Stopwatch.GetTimestamp();
    }
}