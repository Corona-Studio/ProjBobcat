using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using System.Threading.Tasks;

namespace ProjBobcat.Interface
{
    public interface IResourceInfoResolver
    {
        string BasePath { get; set; }
        bool CheckLocalFiles { get; set; }
        VersionInfo VersionInfo { get; set; }
        Task<IEnumerable<IGameResource>> ResolveResourceAsync();
        IEnumerable<IGameResource> ResolveResource();
        int MaxDegreeOfParallelism { get; }

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
    }
}