using System;
using System.IO;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Helper
{
    public static class CryptoHelper
    {
        public static string ComputeFileHash(string path, HashAlgorithm hashAlgorithm)
        {
            using var fs = new FileStream(path, FileMode.Open);
            var retVal = hashAlgorithm.ComputeHash(fs);
            fs.Close();

            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }

        public static string ComputeByteHash(byte[] bytes, HashAlgorithm hashAlgorithm)
        {
            var retVal = hashAlgorithm.ComputeHash(bytes);
            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }
    }
}