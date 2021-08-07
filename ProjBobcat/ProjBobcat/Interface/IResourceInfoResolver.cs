using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IResourceInfoResolver
    {
        string BasePath { get; set; }
        bool CheckLocalFiles { get; set; }
        VersionInfo VersionInfo { get; set; }
        IAsyncEnumerable<IGameResource> ResolveResourceAsync();
        IEnumerable<IGameResource> ResolveResource();

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
    }
}