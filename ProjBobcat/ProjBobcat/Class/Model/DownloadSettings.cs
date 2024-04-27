using System;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

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
        Timeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
        DownloadParts = 16
    };

    public int RetryCount { get; init; }
    public bool CheckFile { get; init; }
    public int Timeout { get; init; }
    public int DownloadParts { get; set; }
    public HashType HashType { get; init; }

    /// <summary>
    /// 认证
    /// </summary>
    public AuthenticationHeaderValue? Authentication { get; init; }

    /// <summary>
    /// 请求源
    /// </summary>
    public string? Host { get; init; }

    public async Task<byte[]> HashDataAsync(Stream stream, CancellationToken? token)
    {
        token ??= CancellationToken.None;
        
        return HashType switch
        {
            HashType.MD5 => await MD5.HashDataAsync(stream, token.Value),
            HashType.SHA1 => await SHA1.HashDataAsync(stream, token.Value),
            HashType.SHA256 => await SHA256.HashDataAsync(stream, token.Value),
            HashType.SHA384 => await SHA384.HashDataAsync(stream, token.Value),
            HashType.SHA512 => await SHA512.HashDataAsync(stream, token.Value),
            _ => throw new NotSupportedException()
        };
    }
}