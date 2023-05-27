using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using ProjBobcat.Class.Model;

#pragma warning disable CA1416

namespace ProjBobcat.Platforms.Windows;

public static class SystemInfoHelper
{
    static readonly PerformanceCounter FreeMemCounter = new("Memory", "Available MBytes");
    static readonly PerformanceCounter MemUsagePercentageCounter = new("Memory", "% Committed Bytes In Use");
    static readonly PerformanceCounter CpuCounter = new("Processor Information", "% Processor Utility", "_Total");

    static SystemInfoHelper()
    {
        // Performance Counter Pre-Heat
        FreeMemCounter.NextValue();
        MemUsagePercentageCounter.NextValue();
        CpuCounter.NextValue();
    }

    /// <summary>
    ///     判断是否安装了 UWP 版本的 Minecraft 。
    /// </summary>
    /// <returns>判断结果。</returns>
    public static bool IsMinecraftUWPInstalled() =>
        !string.IsNullOrEmpty(Get_AppxPackage("Microsoft.MinecraftUWP").Status);

        /// <summary>
    ///     获取 UWP 应用的信息。
    /// </summary>
    /// <param name="appName">应用名称</param>
    /// <returns>GetAppxPackage</returns>
    public static GetAppxPackage Get_AppxPackage(string appName)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe")
            {
                WorkingDirectory = Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Arguments = $"Get-AppxPackage -Name \"{appName}\""
            }
        };
        process.Start();
        var reader = process.StandardOutput;

        Dictionary<string, string> values = new Dictionary<string, string>();
        string[] lines = reader.ReadToEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        string key = null;
        string value = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Contains(":"))
            {
                if (key != null)
                {
                    values[key] = value.TrimEnd();
                }
                string[] parts = line.Split(new[] { ':' }, 2, StringSplitOptions.None);
                key = parts[0].Trim();
                value = parts[1].Trim();
            }
            else if (key != null)
            {
                value += line.Trim();
            }
        }
        if (key != null)
        {
            values[key] = value.TrimEnd();
        }

        GetAppxPackage appxPackage = new GetAppxPackage();
        foreach (KeyValuePair<string, string> pair in values)
        {
            switch (pair.Key)
            {
                case "Name":
                    appxPackage.Name = pair.Value;
                    break;
                case "Publisher":
                    appxPackage.Publisher = pair.Value;
                    break;
                case "Architecture":
                    appxPackage.Architecture = pair.Value;
                    break;
                case "ResourceId":
                    appxPackage.ResourceId = pair.Value;
                    break;
                case "Version":
                    appxPackage.Version = pair.Value;
                    break;
                case "PackageFullName":
                    appxPackage.PackageFullName = pair.Value;
                    break;
                case "InstallLocation":
                    appxPackage.InstallLocation = pair.Value;
                    break;
                case "IsFramework":
                    appxPackage.IsFramework = Convert.ToBoolean(pair.Value);
                    break;
                case "PackageFamilyName":
                    appxPackage.PackageFamilyName = pair.Value;
                    break;
                case "PublisherId":
                    appxPackage.PublisherId = pair.Value;
                    break;
                case "IsResourcePackage":
                    appxPackage.IsResourcePackage = Convert.ToBoolean(pair.Value);
                    break;
                case "IsBundle":
                    appxPackage.IsBundle = Convert.ToBoolean(pair.Value);
                    break;
                case "IsDevelopmentMode":
                    appxPackage.IsDevelopmentMode = Convert.ToBoolean(pair.Value);
                    break;
                case "NonRemovable":
                    appxPackage.NonRemovable = Convert.ToBoolean(pair.Value);
                    break;
                case "Dependencies":
                    appxPackage.Dependencies = pair.Value.TrimStart('{').TrimEnd('}').Split(',').Select(s => s.Trim()).ToArray();
                    break;
                case "IsPartiallyStaged":
                    appxPackage.IsPartiallyStaged = Convert.ToBoolean(pair.Value);
                    break;
                case "SignatureKind":
                    appxPackage.SignatureKind = pair.Value;
                    break;
                case "Status":
                    appxPackage.Status = pair.Value;
                    break;
            }
        }
        return appxPackage;
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

            if(rootReg == null) return Enumerable.Empty<string>();

            using var wow64Reg = rootReg.OpenSubKey("Wow6432Node");

            var javas = FindJavaInternal(rootReg)
                .Union(FindJavaInternal(wow64Reg))
                .ToHashSet();

            return javas;
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public static IEnumerable<string> FindJavaInternal(RegistryKey? registry)
    {
        if(registry == null) return Enumerable.Empty<string>();

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
        var free = FreeMemCounter.NextValue();
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / Math.Pow(1024, 2);
        var used = total - free;
        var percentage = MemUsagePercentageCounter.NextValue();

        var result = new MemoryInfo
        {
            Free = free,
            Percentage = percentage,
            Total = total,
            Used = used
        };

        return result;
    }

    /// <summary>
    ///     获取系统 Cpu 信息
    /// </summary>
    /// <returns></returns>
    public static CPUInfo GetWindowsCpuUsage()
    {
        var percentage = CpuCounter.NextValue();

        return new CPUInfo
        {
            Name = "Total %",
            Usage = percentage
        };
    }

    public static IEnumerable<string> GetLogicalDrives() => Environment.GetLogicalDrives();
}
