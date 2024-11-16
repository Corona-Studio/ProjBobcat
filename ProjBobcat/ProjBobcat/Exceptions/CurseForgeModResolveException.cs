using System;

namespace ProjBobcat.Exceptions;

public class CurseForgeModResolveException : Exception
{
    readonly long _addonId, _fileId;
    readonly string? _moreInfo;

    public CurseForgeModResolveException(long addonId, long fileId)
    {
        this._addonId = addonId;
        this._fileId = fileId;
    }

    public CurseForgeModResolveException(long addonId, long fileId, string moreInfo)
    {
        this._addonId = addonId;
        this._fileId = fileId;
        this._moreInfo = moreInfo;
    }

    public override string ToString()
    {
        return $$"""

                 无法解析一个或多个 CurseForge 模组，可能的原因是因为该模组已近被作者删除或是该整合包所需的该模组的文件已经被删除。
                 模组文件下载链接：https://api.curseforge.com/v1/mods/{{this._addonId}}/files/{{this._fileId}}/download-url
                 {{this._moreInfo}}
                 """;
    }
}