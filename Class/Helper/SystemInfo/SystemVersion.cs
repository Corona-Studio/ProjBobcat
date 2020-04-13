using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper.SystemInfo
{
    public class WindowsSystemVersion : IFormattable, IEquatable<WindowsSystemVersion>
    {
        private byte version;
        private WindowsSystemVersion() { }

        public static WindowsSystemVersion Unknown { get; }
            = new WindowsSystemVersion() { version = 0 };
        public static WindowsSystemVersion WindowsXP { get; }
            = new WindowsSystemVersion() { version = 1 };
        public static WindowsSystemVersion Windows2003 { get; }
            = new WindowsSystemVersion() { version = 2 };
        public static WindowsSystemVersion Windows2008 { get; }
            = new WindowsSystemVersion() { version = 3 };
        public static WindowsSystemVersion Windows7 { get; }
            = new WindowsSystemVersion() { version = 4 };
        public static WindowsSystemVersion Windows8 { get; }
            = new WindowsSystemVersion() { version = 5 };
        public static WindowsSystemVersion Windows8Dot1 { get; }
            = new WindowsSystemVersion() { version = 6 };
        public static WindowsSystemVersion Windows10 { get; }
            = new WindowsSystemVersion() { version = 7 };

        public override bool Equals(object obj)
        {
            if (obj is WindowsSystemVersion systemVersion)
            {
                return version == systemVersion.version;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return version;
        }
        public bool Equals(WindowsSystemVersion other)
            => version == other.version;

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
        /// 
        /// </summary>
        /// <param name="format">根据 <see cref="String.Format(string, object)"/> 的中第一个参数的格式，其中的 "{0}" 会被替换为对应字符串。</param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public string ToString(string format, IFormatProvider formatProvider = null)
        {
            return string.Format(format, this.ToString());
        }

        public static bool operator ==(WindowsSystemVersion left, WindowsSystemVersion right)
            => left?.version == right?.version;

        public static bool operator !=(WindowsSystemVersion left, WindowsSystemVersion right)
            => left?.version != right?.version;
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
