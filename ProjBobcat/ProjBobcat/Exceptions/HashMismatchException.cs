using System;

namespace ProjBobcat.Exceptions;

public sealed class HashMismatchException : Exception
{
    public HashMismatchException(string filePath, string expectedHash, string actualHash) : base(GetMessage(filePath, expectedHash, actualHash))
    {
        FilePath = filePath;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }

    public string FilePath { get; }
    public string ExpectedHash { get; }
    public string ActualHash { get; }

    static string GetMessage(string filePath, string expectedHash, string actualHash)
    {
        return $"""
                文件 {filePath} 的哈希值不匹配。
                期望哈希值：{expectedHash}
                实际哈希值：{actualHash}
                """;
    }
}