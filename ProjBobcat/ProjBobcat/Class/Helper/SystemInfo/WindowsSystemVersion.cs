using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper.SystemInfo
{
    /// <summary>
    /// 表示一个 Windows 系统版本。
    /// </summary>
    public class WindowsSystemVersion : IFormattable, IEquatable<WindowsSystemVersion>
    {
        private byte version;
        private WindowsSystemVersion() { }

        /// <summary>
        /// 未知。
        /// </summary>
        public static WindowsSystemVersion Unknown { get; }
            = new WindowsSystemVersion() { version = 0 };
        /// <summary>
        /// XP 。
        /// </summary>
        public static WindowsSystemVersion WindowsXP { get; }
            = new WindowsSystemVersion() { version = 1 };
        /// <summary>
        /// 2003 。
        /// </summary>
        public static WindowsSystemVersion Windows2003 { get; }
            = new WindowsSystemVersion() { version = 2 };
        /// <summary>
        /// 2008 。
        /// </summary>
        public static WindowsSystemVersion Windows2008 { get; }
            = new WindowsSystemVersion() { version = 3 };
        /// <summary>
        /// 7 。
        /// </summary>
        public static WindowsSystemVersion Windows7 { get; }
            = new WindowsSystemVersion() { version = 4 };
        /// <summary>
        /// 8 。
        /// </summary>
        public static WindowsSystemVersion Windows8 { get; }
            = new WindowsSystemVersion() { version = 5 };
        /// <summary>
        /// 8.1 。
        /// </summary>
        public static WindowsSystemVersion Windows8Dot1 { get; }
            = new WindowsSystemVersion() { version = 6 };
        /// <summary>
        /// 10 。
        /// </summary>
        public static WindowsSystemVersion Windows10 { get; }
            = new WindowsSystemVersion() { version = 7 };

        /// <summary>
        /// 判断一个对象是否与当前实例相同。
        /// </summary>
        /// <param name="obj">要判断的对象。</param>
        /// <returns>判断结果。</returns>
        public override bool Equals(object obj)
        {
            if (obj is WindowsSystemVersion systemVersion)
            {
                return version == systemVersion.version;
            }
            return false;
        }
        /// <summary>
        /// 获取当前实例的哈希值。
        /// </summary>
        /// <returns>当前实例的哈希值。</returns>
        public override int GetHashCode()
        {
            return version;
        }
        /// <summary>
        /// 判断一个 <see cref="WindowsSystemVersion"/> 是否与当前实例相同。
        /// </summary>
        /// <param name="other">要判断的 <see cref="WindowsSystemVersion"/> 。</param>
        /// <returns>判断结果。</returns>
        public bool Equals(WindowsSystemVersion other)
            => version == other.version;

        /// <summary>
        /// 返回一个表示当前实例的字符串。
        /// 这将得到如 "8.1" 、 "8" 、 "2003" 、 "XP" 、 "unknown" 等。
        /// </summary>
        /// <returns>对应的字符串。</returns>
        public override string ToString()
        {
            return (version) switch
            {
                7 => "10",
                6 => "8.1",
                5 => "8",
                4 => "7",
                3 => "2008",
                2 => "2003",
                1 => "XP",
                0 => "unknown",
                _ => "unknown",
            };
        }
        /// <summary>
        /// 根据指定格式返回一个表示当前实例的字符串。
        /// </summary>
        /// <param name="format">根据 <see cref="String.Format(string, object)"/> 的中第一个参数的格式，其中的 "{0}" 会被替换为对应字符串。</param>
        /// <param name="formatProvider">此参数将被忽略。</param>
        /// <returns>对应的字符串。</returns>
        public string ToString(string format, IFormatProvider formatProvider = null)
        {
            return format == null ? this.ToString() :
                string.Format(format, this.ToString());
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(WindowsSystemVersion left, WindowsSystemVersion right)
            => left?.version == right?.version;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(WindowsSystemVersion left, WindowsSystemVersion right)
            => left?.version != right?.version;
        /// <summary>
        /// 获取当前程序运行所在的系统版本。
        /// </summary>
        public static WindowsSystemVersion CurrentVersion
            => (Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor) switch
            {
                "10.0" => Windows10,
                "6.3" => Windows8Dot1,
                "6.2" => Windows8,
                "6.1" => Windows7,
                "6.0" => Windows2008,
                "5.2" => Windows2003,
                "5.1" => WindowsXP,
                _ => Unknown
            };
    }
}
