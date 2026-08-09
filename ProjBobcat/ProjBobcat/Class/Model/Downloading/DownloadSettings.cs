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
    /// <summary>
    ///     Maximum number of attempts for a request. Values less than one use the adaptive default.
    /// </summary>
    public int RetryCount { get; init; }
    public bool CheckFile { get; init; }

    /// <summary>
    ///     Maximum inactivity timeout. It no longer limits the duration of the complete file download.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Maximum connections for one file. The downloader automatically selects the actual count.
    ///     Zero uses the default maximum of 16.
    /// </summary>
    public int DownloadParts { get; init; }

    /// <summary>
    ///     Connection budget shared by a list download.
    /// </summary>
    public int DownloadThread { get; init; } = Environment.ProcessorCount;
    public HashType HashType { get; init; }
    public bool ShowDownloadProgress { get; init; }

    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(12);
    public required IHttpClientFactory HttpClientFactory { get; init; }
    public string? HttpClientName { get; init; }

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
}
