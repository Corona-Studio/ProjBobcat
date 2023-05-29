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
    public static bool IsMinecraftUWPInstalled()
    {
        return !string.IsNullOrEmpty(GetAppxPackage("Microsoft.MinecraftUWP").Status);
    }

    /// <summary>
    ///     获取 UWP 应用的信息。
    /// </summary>
    /// <param name="appName">应用名称</param>
    /// <returns>GetAppxPackage</returns>
    public static AppxPackageInfo GetAppxPackage(string appName)
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

        var values = ParseAppxPackageOutput(reader.ReadToEnd());

        var appxPackageInfo = new AppxPackageInfo();
        return SetAppxPackageInfoProperty(appxPackageInfo, values);
    }

    /// <summary>
    ///     分析 PowerShell Get-AppxPackage 的输出。
    /// </summary>
    /// <param name="output">PowerShell Get-AppxPackage 的输出</param>
    /// <returns>分析完毕的 Dictionary</returns>
    static Dictionary<string, string> ParseAppxPackageOutput(string output)
    {
        var values = new Dictionary<string, string>();
        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        string key = null;
        string value = null;
        foreach (var line in lines)
            if (line.Contains(":"))
            {
                if (key != null) values[key] = value.TrimEnd();
                var parts = line.Split(new[] { ':' }, 2, StringSplitOptions.None);
                key = parts[0].Trim();
                value = parts[1].Trim();
            }
            else if (key != null)
            {
                value += line.Trim();
            }

        if (key != null) values[key] = value.TrimEnd();

        return values;
    }

    /// <summary>
    ///     设置 AppxPackageInfo 的属性。
    /// </summary>
    /// <param name="appxPackageInfo">要设置的 AppxPackageInfo</param>
    /// <param name="values">分析完毕的 Dictionary</param>
    static AppxPackageInfo SetAppxPackageInfoProperty(AppxPackageInfo appxPackageInfo,
        Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            appxPackageInfo = key switch
            {
                "Name" => appxPackageInfo with { Name = value },
                "Publisher" => appxPackageInfo with { Publisher = value },
                "Architecture" => appxPackageInfo with { Architecture = value },
                "ResourceId" => appxPackageInfo with { ResourceId = value },
                "Version" => appxPackageInfo with { Version = value },
                "PackageFullName" => appxPackageInfo with { PackageFullName = value },
                "InstallLocation" => appxPackageInfo with { InstallLocation = value },
                "IsFramework" => appxPackageInfo with { IsFramework = Convert.ToBoolean(value) },
                "PackageFamilyName" => appxPackageInfo with { PackageFamilyName = value },
                "PublisherId" => appxPackageInfo with { PublisherId = value },
                "IsResourcePackage" => appxPackageInfo with { IsResourcePackage = Convert.ToBoolean(value) },
                "IsBundle" => appxPackageInfo with { IsBundle = Convert.ToBoolean(value) },
                "IsDevelopmentMode" => appxPackageInfo with { IsDevelopmentMode = Convert.ToBoolean(value) },
                "NonRemovable" => appxPackageInfo with { NonRemovable = Convert.ToBoolean(value) },
                "Dependencies" => appxPackageInfo with
                {
                    Dependencies = value.TrimStart('{').TrimEnd('}').Split(',')
                        .Select(s => s.Trim()).ToArray()
                },
                "IsPartiallyStaged" => appxPackageInfo with { IsPartiallyStaged = Convert.ToBoolean(value) },
                "SignatureKind" => appxPackageInfo with { SignatureKind = value },
                "Status" => appxPackageInfo with { Status = value },
                _ => appxPackageInfo
            };

        return appxPackageInfo;
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

            if (rootReg == null) return Enumerable.Empty<string>();

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
        if (registry == null) return Enumerable.Empty<string>();

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

    public static IEnumerable<string> GetLogicalDrives()
    {
        return Environment.GetLogicalDrives();
    }
}
