using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     加密助手
/// </summary>
public static class CryptoHelper
{
    public static string ToString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    /// <summary>
    ///     计算文件 Hash 值
    /// </summary>
    /// <param name="path"></param>
    /// <param name="hashAlgorithm"></param>
    /// <returns></returns>
    public static async Task<string> ComputeFileHashAsync(Stream stream, HashAlgorithm hashAlgorithm)
    {
        var retVal = await hashAlgorithm.ComputeHashAsync(stream);

        return BitConverter.ToString(retVal).Replace("-", string.Empty);
    }

    /// <summary>
    ///     计算文件 Hash 值
    /// </summary>
    /// <param name="path"></param>
    /// <param name="hashAlgorithm"></param>
    /// <returns></returns>
    public static async Task<string> ComputeFileHashAsync(string path, HashAlgorithm hashAlgorithm)
    {
        await using var fs = File.Open(path, FileMode.Open, FileAccess.Read);
        var retVal = await hashAlgorithm.ComputeHashAsync(fs);

        return BitConverter.ToString(retVal).Replace("-", string.Empty);
    }

    /// <summary>
    ///     计算文件 Hash 值
    /// </summary>
    /// <param name="path"></param>
    /// <param name="hashAlgorithm"></param>
    /// <returns></returns>
    public static string ComputeFileHash(string path, HashAlgorithm hashAlgorithm)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read);
        var retVal = hashAlgorithm.ComputeHash(fs);

        return BitConverter.ToString(retVal).Replace("-", string.Empty);
    }

    /// <summary>
    ///     计算字节数组 Hash 值
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="hashAlgorithm"></param>
    /// <returns></returns>
    public static string ComputeByteHash(byte[] bytes, HashAlgorithm hashAlgorithm)
    {
        var retVal = hashAlgorithm.ComputeHash(bytes);
        return BitConverter.ToString(retVal).Replace("-", string.Empty);
    }
}