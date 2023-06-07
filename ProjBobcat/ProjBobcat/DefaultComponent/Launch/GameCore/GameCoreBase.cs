using System;
using System.ComponentModel;
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
    bool disposedValue;
    public virtual string? RootPath { get; set; }
    public virtual Guid ClientToken { get; set; }
    public virtual VersionLocatorBase VersionLocator { get; set; }
    public virtual IGameLogResolver GameLogResolver { get; set; }

    public event EventHandler<GameExitEventArgs> GameExitEventDelegate
    {
        add => ListEventDelegates.AddHandler(GameExitEventKey, value);
        remove => ListEventDelegates.RemoveHandler(GameExitEventKey, value);
    }

    public event EventHandler<GameLogEventArgs> GameLogEventDelegate
    {
        add => ListEventDelegates.AddHandler(GameLogEventKey, value);
        remove => ListEventDelegates.RemoveHandler(GameLogEventKey, value);
    }

    public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate
    {
        add => ListEventDelegates.AddHandler(LaunchLogEventKey, value);
        remove => ListEventDelegates.RemoveHandler(LaunchLogEventKey, value);
    }

    /// <summary>
    ///     启动 （同步方法）
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public virtual LaunchResult Launch(LaunchSettings? settings)
    {
        return LaunchTaskAsync(settings).Result;
    }

    /// <summary>
    ///     启动 （异步方法）
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public abstract Task<LaunchResult> LaunchTaskAsync(LaunchSettings? settings);

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~GameCoreBase()
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

    public virtual void OnGameExit(object sender, GameExitEventArgs e)
    {
        var eventList = ListEventDelegates;
        var @event = (EventHandler<GameExitEventArgs>)eventList[GameExitEventKey]!;
        @event?.Invoke(sender, e);
    }

    public virtual void OnLogGameData(object sender, GameLogEventArgs e)
    {
        var eventList = ListEventDelegates;
        var @event = (EventHandler<GameLogEventArgs>)eventList[GameLogEventKey]!;
        @event?.Invoke(sender, e);
    }

    public virtual void OnLogLaunchData(object sender, LaunchLogEventArgs e)
    {
        var eventList = ListEventDelegates;
        var @event = (EventHandler<LaunchLogEventArgs>)eventList[LaunchLogEventKey]!;
        @event?.Invoke(sender, e);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) ListEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}