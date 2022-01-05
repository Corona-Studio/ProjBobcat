using System;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Model;

public enum HashType
{
    MD5,
    SHA1,
    SHA256,
    SHA384,
    SHA512
}

public class DownloadSettings
{
    public static DownloadSettings Default => new()
    {
        RetryCount = 0,
        CheckFile = false,
        Timeout = (int) TimeSpan.FromMinutes(5).TotalMilliseconds,
        DownloadParts = 8
    };

    public int RetryCount { get; init; }
    public bool CheckFile { get; init; }
    public int Timeout { get; init; }
    public int DownloadParts { get; set; }
    public HashType HashType { get; init; }

    public HashAlgorithm GetHashAlgorithm()
    {
        return HashType switch
        {
            HashType.MD5 => MD5.Create(),
            HashType.SHA1 => SHA1.Create(),
            HashType.SHA256 => SHA256.Create(),
            HashType.SHA384 => SHA384.Create(),
            HashType.SHA512 => SHA512.Create(),
            _ => throw new NotSupportedException()
        };
    }
}