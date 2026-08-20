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
                The hash of file {filePath} does not match.
                Expected hash: {expectedHash}
                Actual hash: {actualHash}
                """;
    }
}
