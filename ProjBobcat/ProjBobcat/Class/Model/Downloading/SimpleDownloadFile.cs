namespace ProjBobcat.Class.Model.Downloading;

/// <summary>
///     下载文件信息类
/// </summary>
public sealed class SimpleDownloadFile : AbstractDownloadBase
{
    public required string DownloadUri { get; init; }

    public override string GetDownloadUrl() => DownloadUri;
}