using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public abstract class ResolverBase : IResourceInfoResolver
    {
        protected EventHandlerList listEventDelegates = new();
        bool disposedValue;
        static readonly object ResolveEventKey = new();

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent
        {
            add
            {
                listEventDelegates.AddHandler(ResolveEventKey, value);
            }
            remove
            {
                listEventDelegates.RemoveHandler(ResolveEventKey, value);
            }
        }

        public string BasePath { get; set; }
        public bool CheckLocalFiles { get; set; }
        public VersionInfo VersionInfo { get; set; }

        public int MaxDegreeOfParallelism { get; init; }

        public virtual void OnResolve(string currentStatus, double progress = 0)
        {
            var eventList = listEventDelegates;
            var @event = (EventHandler<GameResourceInfoResolveEventArgs>)eventList[ResolveEventKey]!;

            if (string.IsNullOrEmpty(currentStatus))
            {
                @event?.Invoke(this, new GameResourceInfoResolveEventArgs
                {
                    Progress = progress
                });
            }

            @event?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                Status = currentStatus,
                Progress = progress
            });
        }

        public abstract Task<IEnumerable<IGameResource>> ResolveResourceAsync();

        public virtual IEnumerable<IGameResource> ResolveResource()
        {
            return ResolveResourceAsync().Result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    listEventDelegates.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~ResolverBase()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
