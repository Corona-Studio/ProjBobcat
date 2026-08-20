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
                Failed to resolve one or more CurseForge mods. The mod or the file required by the modpack may have been deleted by its author.
                Mod file download URL: https://api.curseforge.com/v1/mods/{addonId}/files/{fileId}/download-url
                {moreInfo}
                """;
    }
}
