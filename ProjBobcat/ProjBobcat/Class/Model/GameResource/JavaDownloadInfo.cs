using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.GameResource;

public class JavaDownloadInfo : IGameResource
{
    public required string FileName { get; init; }
    public required string Path { get; init; }
    public required string Title { get; init; }
    public required ResourceType Type { get; init; }
    public required string Url { get; init; }
    public required long FileSize { get; init; }
    public required string? CheckSum { get; init; }
}