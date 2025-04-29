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
    readonly EventHandlerList _listEventDelegates = new();

    public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent
    {
        add => this._listEventDelegates.AddHandler(ResolveEventKey, value);
        remove => this._listEventDelegates.RemoveHandler(ResolveEventKey, value);
    }

    public abstract IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame);

    public virtual IEnumerable<IGameResource> ResolveResource(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame)
    {
        return this.ResolveResourceAsync(basePath, checkLocalFiles, resolvedGame).ToListAsync().GetAwaiter()
            .GetResult();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public virtual void OnResolve(string currentStatus, ProgressValue progress)
    {
        var eventList = this._listEventDelegates;
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
}