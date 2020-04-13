using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper.SystemInfo
{
    public class SystemArch : IFormattable, IEquatable<SystemArch>
    {
        private bool is64BitOperatingSystem;
        public static SystemArch X64 { get; } = new SystemArch() { is64BitOperatingSystem = true };
        public static SystemArch X86 { get; } = new SystemArch() { is64BitOperatingSystem = false };

        public override bool Equals(object obj)
        {
            if (obj is SystemArch arch)
            {
                return is64BitOperatingSystem == arch.is64BitOperatingSystem;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return is64BitOperatingSystem ? 0 : 1;
        }
        public bool Equals(SystemArch other)
            => is64BitOperatingSystem == other.is64BitOperatingSystem;

        public override string ToString()
        {
            return is64BitOperatingSystem ? "x64" : "x86";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="format">根据 <see cref="String.Format(string, object)"/> 的中第一个参数的格式，其中的 "{0}" 会被替换为 "64" 或者 "86"。</param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public string ToString(string format, IFormatProvider formatProvider = null)
        {
            return string.Format(format, is64BitOperatingSystem ? "64" : "86");
        }

        public static bool operator ==(SystemArch left, SystemArch right)
            => left?.is64BitOperatingSystem == right?.is64BitOperatingSystem;

        public static bool operator !=(SystemArch left, SystemArch right)
            => left?.is64BitOperatingSystem != right?.is64BitOperatingSystem;

    }
}
