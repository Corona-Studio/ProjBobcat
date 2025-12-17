using System;
using System.Collections.Generic;
using System.Threading;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

public interface IResourceInfoResolver : IDisposable
{
    IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame,
        CancellationToken cancellationToken = default);

    IEnumerable<IGameResource> ResolveResource(string basePath, bool checkLocalFiles, ResolvedGameVersion resolvedGame);

    event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
}