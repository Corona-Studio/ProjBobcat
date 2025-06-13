using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Model.Downloading;

// ReSharper disable InconsistentNaming

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
    public int RetryCount { get; init; }
    public bool CheckFile { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int DownloadParts { get; init; }
    public int DownloadThread { get; init; } = Environment.ProcessorCount;
    public HashType HashType { get; init; }
    public bool ShowDownloadProgress { get; init; }
    public required IHttpClientFactory HttpClientFactory { get; init; }

    /// <summary>
    ///     认证
    /// </summary>
    public AuthenticationHeaderValue? Authentication { get; init; }

    /// <summary>
    ///     请求源
    /// </summary>
    public string? Host { get; init; }

    internal HashAlgorithm GetCryptoTransform()
    {
        return this.HashType switch
        {
            HashType.MD5 => MD5.Create(),
            HashType.SHA1 => SHA1.Create(),
            HashType.SHA256 => SHA256.Create(),
            HashType.SHA384 => SHA384.Create(),
            HashType.SHA512 => SHA512.Create(),
            _ => throw new NotSupportedException()
        };
    }

    public static DownloadSettings FromDefault(IHttpClientFactory httpClientFactory)
    {
        return new DownloadSettings
        {
            RetryCount = 0,
            CheckFile = false,
            Timeout = TimeSpan.FromMinutes(1),
            DownloadParts = 16,
            HttpClientFactory = httpClientFactory
        };
    }
}