using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using System;
using System.Collections.Generic;

namespace ProjBobcat.Interface
{
    public interface IResourceInfoResolver
    {
        string BasePath { get; set; }
        VersionInfo VersionInfo { get; set; }
        IAsyncEnumerable<IGameResource> ResolveResourceAsync();
        IEnumerable<IGameResource> ResolveResource();

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
    }
}