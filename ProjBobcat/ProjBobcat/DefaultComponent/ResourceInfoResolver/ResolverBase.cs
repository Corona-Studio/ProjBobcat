using System;
using System.Collections.Generic;
using System.ComponentModel;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public abstract class ResolverBase : IResourceInfoResolver
{
    static readonly object ResolveEventKey = new();
    readonly EventHandlerList listEventDelegates = new();
    bool disposedValue;

    public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent
    {
        add => listEventDelegates.AddHandler(ResolveEventKey, value);
        remove => listEventDelegates.RemoveHandler(ResolveEventKey, value);
    }

    public string BasePath { get; set; }
    public bool CheckLocalFiles { get; set; }
    public VersionInfo VersionInfo { get; set; }

    public abstract IAsyncEnumerable<IGameResource> ResolveResourceAsync();

    public virtual IEnumerable<IGameResource> ResolveResource()
    {
        return ResolveResourceAsync().ToListAsync().Result;
    }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void OnResolve(string currentStatus, double progress = 0)
    {
        var eventList = listEventDelegates;
        var @event = (EventHandler<GameResourceInfoResolveEventArgs>)eventList[ResolveEventKey]!;

        if (string.IsNullOrEmpty(currentStatus))
            @event?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                Progress = progress
            });

        @event?.Invoke(this, new GameResourceInfoResolveEventArgs
        {
            Status = currentStatus,
            Progress = progress
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) listEventDelegates.Dispose();

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }
}