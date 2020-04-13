using System;
using System.Security.Cryptography;
using System.Text;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// Guid 帮助器。
    /// </summary>
    public static class GuidHelper
    {
        /// <summary>
        /// 还原经过哈希算法（ MD5 ）的Guid。 
        /// </summary>
        /// <param name="str">要还原的 Guid 。</param>
        /// <returns>还原结果。</returns>
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