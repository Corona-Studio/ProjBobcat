using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IResourceInfoResolver
    {
        string BasePath { get; set; }
        VersionInfo VersionInfo { get; set; }
        Task<IEnumerable<IGameResource>> ResolveResourceTaskAsync();
        IEnumerable<IGameResource> ResolveResource();

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
    }
}