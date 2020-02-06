using System;
using System.Security.Cryptography;
using System.Text;

namespace ProjBobcat.Class.Helper
{
    public static class GuidHelper
    {
        public static Guid ToGuid(this string str)
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
            return new Guid(data);
        }

        public static Guid GetGuidByName(string username)
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes($"OfflinePlayer:{username}"));
            return new Guid(data);
        }
    }
}