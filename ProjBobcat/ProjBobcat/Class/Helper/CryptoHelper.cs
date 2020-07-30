using System;
using System.IO;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Helper
{
    public static class CryptoHelper
    {
        public static string ComputeFileHash(string path, HashAlgorithm hashAlgorithm)
        {
            using var x = MD5.Create();
            using var fs = new FileStream(path, FileMode.Open);
            var retVal = hashAlgorithm.ComputeHash(fs);

            return BitConverter.ToString(retVal).Replace("-", string.Empty);
        }
    }
}