using System;

namespace ProjBobcat.Exceptions;

public sealed class HashMismatchException(
    string filePath,
    string expectedHash,
    string actualHash)
    : Exception(GetMessage(filePath, expectedHash, actualHash))
{
    static string GetMessage(string filePath, string expectedHash, string actualHash)
    {
        return $"""
                文件 {filePath} 的哈希值不匹配。
                期望哈希值：{expectedHash}
                实际哈希值：{actualHash}
                """;
    }
}