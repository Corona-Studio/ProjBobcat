using System;
using System.IO;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Helper
{
    public static class CryptoHelper
    {
        public static string ComputeFileHash(string path, HashAlgorithm hashAlgorithm)
        {
            var bytes = File.ReadAllBytes(path);
            var retVal = hashAlgorithm.ComputeHash(bytes);

            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }

        public static string ComputeByteHash(byte[] bytes, HashAlgorithm hashAlgorithm)
        {
            var retVal = hashAlgorithm.ComputeHash(bytes);
            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }
    }
}