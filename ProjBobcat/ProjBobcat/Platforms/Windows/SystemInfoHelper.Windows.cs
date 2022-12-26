using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Microsoft.Win32;
using ProjBobcat.Class.Model;

#pragma warning disable CA1416

namespace ProjBobcat.Platforms.Windows;

class SystemInfoHelper
{
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
    ///     从注册表中查找可能的 javaw.exe 的路径。
    /// </summary>
    /// <returns>可能的 Java 路径构成的列表。</returns>
    public static IEnumerable<string> FindJavaWindows()
    {
        try
        {
            using var rootReg = Registry.LocalMachine.OpenSubKey("SOFTWARE");
            using var wow64Reg = rootReg.OpenSubKey("Wow6432Node");

            var javas = (rootReg == null ? Array.Empty<string>() : FindJavaInternal(rootReg))
                .Union(FindJavaInternal(wow64Reg))
                .ToHashSet();

            return javas;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static IEnumerable<string> FindJavaInternal(RegistryKey registry)
    {
        try
        {
            using var regKey = registry.OpenSubKey("JavaSoft");
            using var javaRuntimeReg = regKey?.OpenSubKey("Java Runtime Environment");

            if (javaRuntimeReg == null)
                return Enumerable.Empty<string>();

            var result = new List<string>();
            foreach (var ver in javaRuntimeReg.GetSubKeyNames())
            {
                var versions = javaRuntimeReg.OpenSubKey(ver);
                var javaHomes = versions?.GetValue("JavaHome");

                if (javaHomes == null) continue;

                var str = javaHomes?.ToString();

                if (string.IsNullOrWhiteSpace(str)) continue;

                result.Add($"{str}\\bin\\javaw.exe");
            }

            return result;
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
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
    public static IEnumerable<CPUInfo> GetWindowsCpuUsage()
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

    public static IEnumerable<string> GetLogicalDrives()
    {
        return Environment.GetLogicalDrives();
    }
}