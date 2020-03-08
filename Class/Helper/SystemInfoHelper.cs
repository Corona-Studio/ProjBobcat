using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Runspaces;
using Microsoft.Win32;

namespace ProjBobcat.Class.Helper
{
    public static class SystemInfoHelper
    {
        /// <summary>
        ///     Detect javaw.exe via reg.
        ///     从注册表中查找可能的javaw.exe位置
        /// </summary>
        /// <returns>A list, containing all possible path of javaw.exe. JAVA地址列表。</returns>
        public static IEnumerable<string> FindJava()
        {
            try
            {
                var rootReg = Registry.LocalMachine.OpenSubKey("SOFTWARE");
                return rootReg == null
                    ? new string[0]
                    : FindJavaInternal(rootReg).Union(FindJavaInternal(rootReg.OpenSubKey("Wow6432Node")));
            }
            catch
            {
                return new string[0];
            }
        }

        private static IEnumerable<string> FindJavaInternal(RegistryKey registry)
        {
            try
            {
                var registryKey = registry.OpenSubKey("JavaSoft");
                if (registryKey == null || (registry = registryKey.OpenSubKey("Java Runtime Environment")) == null)
                    return new string[0];
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
                return new string[0];
            }
        }


        public static string GetSystemArch()
        {
            return Environment.Is64BitOperatingSystem ? "64" : "86";
        }

        public static string GetSystemVersion()
        {
            return (Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor) switch
            {
                "10.0" => "10",
                "6.3" => "8.1",
                "6.2" => "8",
                "6.1" => "7",
                "6.0" => "2008",
                "5.2" => "2003",
                "5.1" => "XP",
                _ => "unknown"
            };
        }

        public static bool IsMinecraftUWPInstalled()
        {
            var rs = RunspaceFactory.CreateRunspace();
            rs.Open();
            var pl = rs.CreatePipeline();
            pl.Commands.AddScript("Get-AppxPackage -Name \"Microsoft.MinecraftUWP\"");
            pl.Commands.Add("Out-String");
            var result = pl.Invoke();
            rs.Close();
            rs.Dispose();
            if (result == null || string.IsNullOrEmpty(result[0].ToString())) return false;
            return true;
        }
    }
}