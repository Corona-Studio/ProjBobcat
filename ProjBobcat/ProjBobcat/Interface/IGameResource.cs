using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

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
    ///     Urls
    /// </summary>
    IReadOnlyList<DownloadUriInfo> Urls { get; init; }

    string FileName { get; init; }

    long FileSize { get; init; }

    string? CheckSum { get; init; }
}