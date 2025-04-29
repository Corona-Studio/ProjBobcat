using System;

namespace ProjBobcat.Exceptions;

public sealed class CurseForgeModResolveException : AggregateException
{
    public CurseForgeModResolveException(long addonId, long fileId) : base(GetMessage(addonId, fileId, null))
    {
        this.AddonId = addonId;
        this.FileId = fileId;
    }

    public CurseForgeModResolveException(long addonId, long fileId, Exception ex) : base(
        new Exception(GetMessage(addonId, fileId, null)), ex)
    {
        this.AddonId = addonId;
        this.FileId = fileId;
    }

    public CurseForgeModResolveException(long addonId, long fileId, string moreInfo) : base(GetMessage(addonId, fileId,
        moreInfo))
    {
        this.AddonId = addonId;
        this.FileId = fileId;
        this.MoreInfo = moreInfo;
    }

    public long AddonId { get; }

    public long FileId { get; }

    public string? MoreInfo { get; }

    static string GetMessage(long addonId, long fileId, string? moreInfo)
    {
        return $"""
                无法解析一个或多个 CurseForge 模组，可能的原因是因为该模组已近被作者删除或是该整合包所需的该模组的文件已经被删除。
                模组文件下载链接：https://api.curseforge.com/v1/mods/{addonId}/files/{fileId}/download-url
                {moreInfo}
                """;
    }
}