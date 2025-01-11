using ProjBobcat.Interface;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.GameResource;

public class GameLoggingDownloadInfo : IGameResource
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public required ResourceType Type { get; init; }
    public required IReadOnlyList<string> Urls { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required string? CheckSum { get; init; }
}