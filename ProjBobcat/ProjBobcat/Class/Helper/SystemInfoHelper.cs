using Microsoft.Win32;
using ProjBobcat.Class.Helper.SystemInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     系统信息帮助器。
    /// </summary>
    public static class SystemInfoHelper
    {
        /// <summary>
        ///     从注册表中查找可能的 javaw.exe 的路径。
        /// </summary>
        /// <returns>可能的 Java 路径构成的列表。</returns>
        public static IEnumerable<string> FindJava()
        {
            try
            {
                using var rootReg = Registry.LocalMachine.OpenSubKey("SOFTWARE");
                return rootReg == null
                    ? Array.Empty<string>()
                    : FindJavaInternal(rootReg).Union(FindJavaInternal(rootReg.OpenSubKey("Wow6432Node")));
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> FindJavaInternal(RegistryKey registry)
        {
            try
            {
                using var registryKey = registry.OpenSubKey("JavaSoft");
                if (registryKey == null || (registry = registryKey.OpenSubKey("Java Runtime Environment")) == null)
                    return Array.Empty<string>();
                return from ver in registry.GetSubKeyNames()
                    select registry.OpenSubKey(ver)
                    into command
                    where command != null
                    select command.GetValue("JavaHome")
                    into javaHomes
                    where javaHomes != null
                    select javaHomes.ToString()
                    into str
                    where !string.IsNullOrWhiteSpace(str)
                    select str + @"\bin\javaw.exe";
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        ///     获取当前程序运行所在的系统架构。
        /// </summary>
        /// <returns>当前程序运行所在的系统架构。</returns>
        [Obsolete("已过时，使用 ProjBobcat.Class.Helper.SystemInfo.SystemArch.CurrentArch 属性替代。")]
        public static SystemArch GetSystemArch()
        {
            return SystemArch.CurrentArch;
        }

        /// <summary>
        ///     获取当前程序运行所在的系统版本。
        /// </summary>
        /// <returns>当前程序运行所在的系统版本。</returns>
        [Obsolete("已过时，使用 ProjBobcat.Class.Helper.SystemInfo.WindowsSystemVersion.CurrentVersion 属性替代。")]
        public static WindowsSystemVersion GetSystemVersion()
        {
            return WindowsSystemVersion.CurrentVersion;
        }

        /// <summary>
        ///     判断是否安装了 UWP 版本的 Minecraft 。
        /// </summary>
        /// <returns>判断结果。</returns>
        public static bool IsMinecraftUWPInstalled()
        {
            using var ps = PowerShell.Create();

            ps.AddScript("Get-AppxPackage -Name \"Microsoft.MinecraftUWP\"");
            var result = ps.Invoke();

            return result != null && !string.IsNullOrEmpty(result[0].ToString());
        }
    }
}