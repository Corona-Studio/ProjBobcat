using System;
using System.IO;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// 加密助手
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// 计算文件 Hash 值
        /// </summary>
        /// <param name="path"></param>
        /// <param name="hashAlgorithm"></param>
        /// <returns></returns>
        public static string ComputeFileHash(string path, HashAlgorithm hashAlgorithm)
        {
            var bytes = File.ReadAllBytes(path);
            var retVal = hashAlgorithm.ComputeHash(bytes);

            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }

        /// <summary>
        /// 计算字节数组 Hash 值
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
}