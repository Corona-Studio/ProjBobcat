using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using ProjBobcat.Class.Helper.SystemInfo;
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
                var javas = (
                    rootReg == null
                        ? Array.Empty<string>()
                        : FindJavaInternal(rootReg)
                            .Union(FindJavaInternal(rootReg.OpenSubKey("Wow6432Node")))
                ).ToList();

                var evJava = FindJavaUsingEnvironmentVariable();

                if (string.IsNullOrEmpty(evJava))
                    return javas;

                if (!javas.Exists(x => x.Equals(evJava, StringComparison.OrdinalIgnoreCase)))
                    javas.Add(Path.Combine(evJava, "bin", "javaw.exe"));

                return javas;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string FindJavaUsingEnvironmentVariable()
        {
            try
            {
                IConfiguration configuration = new ConfigurationBuilder()
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
            var process = new Process
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
        public static Task<MemoryInfo> GetWindowsMemoryStatus()
        {
            return Task.Run(() =>
            {
                var info = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "OS get FreePhysicalMemory,TotalVisibleMemorySize /Value",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEnd();

                var lines = output.Trim().Split("\n");
                var freeMemoryParts = lines[0].Split("=", StringSplitOptions.RemoveEmptyEntries);
                var totalMemoryParts = lines[1].Split("=", StringSplitOptions.RemoveEmptyEntries);

                var total = Math.Round(double.Parse(totalMemoryParts[1]) / 1024, 0);
                var free = Math.Round(double.Parse(freeMemoryParts[1]) / 1024, 0);

                var memoryInfo = new MemoryInfo
                {
                    Total = total,
                    Free = free,
                    Used = total - free
                };

                return memoryInfo;
            });
        }

        /// <summary>
        ///     获取系统 Cpu 信息
        /// </summary>
        /// <returns></returns>
        public static Task<CPUInfo> GetWindowsCpuUsageTask()
        {
            return Task.Run(() =>
            {
                var info = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "CPU get Name,LoadPercentage /Value",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEnd();

                var lines = output.Trim().Split("\n");
                var loadPercentageParts = lines[0].Split("=", StringSplitOptions.RemoveEmptyEntries);
                var nameParts = lines[1].Split("=", StringSplitOptions.RemoveEmptyEntries);

                var usage = double.TryParse(loadPercentageParts[1], out var u) ? u : 0;
                var name = nameParts[1];

                return new CPUInfo
                {
                    Name = name,
                    Usage = usage
                };
            });
        }
    }
}