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
        /// 根据一段字符串生成一个 <see cref="Guid"/> 。
        /// 相等的字符串将产生相等的 <see cref="Guid"/> 。
        /// </summary>
        /// <param name="str">字符串。</param>
        /// <returns>生成结果。</returns>
        public static Guid ToGuid(this string str)
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
            return new Guid(data);
        }
        /// <summary>
        /// 根据离线玩家名生成一个 <see cref="Guid"/> 。
        /// 相等的玩家名将产生相等的 <see cref="Guid"/> 。
        /// </summary>
        /// <param name="username">玩家名。</param>
        /// <returns>生成结果。</returns>
        public static Guid GetGuidByName(string username)
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes($"OfflinePlayer:{username}"));
            return new Guid(data);
        }
        /// <summary>
        /// 得到一个指定格式的新的 Guid 字符串。
        /// 这将产生一个新的 Guid 。
        /// </summary>
        /// <param name="format">
        /// 格式。
        /// 与 <see cref="Guid.ToString(string)"/> 中的参数要求相同，默认为 "N" 。
        /// </param>
        /// <returns>新的 Guid 字符串。</returns>
        public static string NewGuidString(string format = "N")
        {
            return Guid.NewGuid().ToString(format);
        }
    }
}