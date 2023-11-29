using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

public interface IResourceInfoResolver : IDisposable
{
    string BasePath { get; init; }
    bool CheckLocalFiles { get; set; }
    VersionInfo VersionInfo { get; init; }
    IAsyncEnumerable<IGameResource> ResolveResourceAsync();
    IEnumerable<IGameResource> ResolveResource();

    event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
}