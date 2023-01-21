using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IGameResource
{
    /// <summary>
    ///     下载目录
    /// </summary>
    string Path { get; set; }

    /// <summary>
    ///     标题
    /// </summary>
    string Title { get; set; }

    /// <summary>
    ///     文件类型
    /// </summary>
    ResourceType Type { get; set; }

    /// <summary>
    ///     Url
    /// </summary>
    string Url { get; set; }

    string FileName { get; set; }

    long FileSize { get; set; }

    string CheckSum { get; set; }
}