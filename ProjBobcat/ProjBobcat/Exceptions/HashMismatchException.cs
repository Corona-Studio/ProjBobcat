using System;
using System.Linq;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Exceptions;

public sealed class HashMismatchException(
    string filePath,
    string expectedHash,
    string actualHash,
    AbstractDownloadBase downloadBase)
    : Exception(GetMessage(filePath, expectedHash, actualHash, downloadBase))
{
    static string GetMessage(string filePath, string expectedHash, string actualHash, AbstractDownloadBase downloadBase)
    {
        return $"""
                文件 {filePath} 的哈希值不匹配。
                期望哈希值：{expectedHash}
                实际哈希值：{actualHash}
                [{downloadBase.FinishedRangeStreams.Count} files in total]
                [{downloadBase.FinishedRangeStreams.Select(p => p.Value.Length).Sum()}/{downloadBase.UrlInfo?.FileLength ?? 0}]
                {string.Join("\n", downloadBase.FinishedRangeStreams.OrderBy(p => p.Key.Start).Select(p => $"{p.Key} {p.Value.Length}"))}
                """;
    }
}