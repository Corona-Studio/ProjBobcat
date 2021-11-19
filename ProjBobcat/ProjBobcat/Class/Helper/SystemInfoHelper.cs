using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using ProjBobcat.Class.Model;

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
                using var wow64Reg = rootReg.OpenSubKey("Wow6432Node");

                var javas = (rootReg == null ? Array.Empty<string>() : FindJavaInternal(rootReg))
                    .Union(FindJavaInternal(wow64Reg))
                    .Union(FindJavaInOfficialGamePath())
                    .ToHashSet();

                var evJava = FindJavaUsingEnvironmentVariable();

                if (string.IsNullOrEmpty(evJava))
                    return javas;

                javas.Add(Path.Combine(evJava, "bin", "javaw.exe"));

                return javas;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        static IEnumerable<string> FindJavaInOfficialGamePath()
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "runtime");

            var paths = new[] { "java-runtime-alpha", "java-runtime-beta", "jre-legacy" };

            return paths.Select(path => Path.Combine(basePath, path, "bin", "javaw.exe"))
                .Where(File.Exists);
        }

        static string FindJavaUsingEnvironmentVariable()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();
                var javaHome = configuration["JAVA_HOME"];
                return javaHome;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static IEnumerable<string> FindJavaInternal(RegistryKey registry)
        {
            try
            {
                using var registryKey = registry.OpenSubKey("JavaSoft");
                using var javaRuntimeReg = registryKey.OpenSubKey("Java Runtime Environment");

                if (registryKey == null || javaRuntimeReg == null)
                    return Array.Empty<string>();

                return from ver in javaRuntimeReg.GetSubKeyNames()
                    select javaRuntimeReg.OpenSubKey(ver)
                    into command
                    where command != null
                    select command.GetValue("JavaHome")
                    into javaHomes
                    where javaHomes != null
                    select javaHomes.ToString()
                    into str
                    where !string.IsNullOrWhiteSpace(str)
                    select str + "\\bin\\javaw.exe";
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        ///     判断是否安装了 UWP 版本的 Minecraft 。
        /// </summary>
        /// <returns>判断结果。</returns>
        public static bool IsMinecraftUWPInstalled()
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe")
                {
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    Arguments = "Get-AppxPackage -Name \"Microsoft.MinecraftUWP\""
                }
            };

            process.Start();

            var reader = process.StandardOutput;
            return !string.IsNullOrEmpty(reader.ReadToEnd());
        }

        /// <summary>
        ///     获取 系统的内存信息
        /// </summary>
        /// <returns></returns>
        public static MemoryInfo GetWindowsMemoryStatus()
        {
            using var wmiObject = new ManagementObjectSearcher("select * from Win32_OperatingSystem");

            var memoryValue = wmiObject.Get().Cast<ManagementObject>().Select(mo => new
            {
                Free = double.Parse(mo["FreePhysicalMemory"].ToString()) / 1024,
                Total = double.Parse(mo["TotalVisibleMemorySize"].ToString()) / 1024
            }).FirstOrDefault();

            if (memoryValue == default) return null;

            var percent = (memoryValue.Total - memoryValue.Free) / memoryValue.Total;
            var result = new MemoryInfo
            {
                Free = memoryValue.Free,
                Percentage = percent,
                Total = memoryValue.Total,
                Used = memoryValue.Total - memoryValue.Free
            };

            return result;
        }

        /// <summary>
        ///     获取系统 Cpu 信息
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<CPUInfo> GetWindowsCpuUsageTask()
        {
            using var searcher = new ManagementObjectSearcher("select * from Win32_PerfFormattedData_PerfOS_Processor");
            var cpuTimes = searcher.Get()
                .Cast<ManagementObject>()
                .ToDictionary(k => k["Name"].ToString(), v => Convert.ToDouble(v["PercentProcessorTime"]))
                .Select(p => new CPUInfo
                {
                    Name = p.Key,
                    Usage = p.Value
                });

            return cpuTimes;
        }
    }
}