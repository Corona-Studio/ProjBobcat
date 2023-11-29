using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IGameResource
{
    /// <summary>
    ///     下载目录
    /// </summary>
    string Path { get; init; }

    /// <summary>
    ///     标题
    /// </summary>
    string Title { get; init; }

    /// <summary>
    ///     文件类型
    /// </summary>
    ResourceType Type { get; init; }

    /// <summary>
    ///     Url
    /// </summary>
    string Url { get; init; }

    string FileName { get; init; }

    long FileSize { get; init; }

    string? CheckSum { get; init; }
}