using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.SystemInformation;
using Microsoft.Win32;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Platforms.Windows;

[SupportedOSPlatform(nameof(OSPlatform.Windows))]
public static class SystemInfoHelper
{
    static readonly PerformanceCounter? FreeMemCounter;
    static readonly PerformanceCounter? MemUsagePercentageCounter;
    static readonly PerformanceCounter? CpuCounter;

    static readonly string[] LineChArr = ["\r\n", "\r", "\n"];
    static readonly char[] SepArr = [':'];

    static readonly FrozenSet<string> PossibleJavaDirs = new[]
    {
        "java", "jdk", "env", "环境", "run", "软件", "jre", "mc", "soft", "cache", "temp", "corretto", "roaming",
        "users", "craft", "program", "世界", "net", "游戏", "oracle", "game", "file", "data", "jvm", "服务", "server", "客户",
        "client", "整合", "应用", "运行", "前置", "mojang", "官启", "新建文件夹", "eclipse", "microsoft", "hotspot", "runtime", "x86",
        "x64", "forge", "原版", "optifine", "官方", "启动", "hmcl", "mod", "高清", "download", "launch", "程序", "path",
        "version", "baka", "pcl", "zulu", "local", "packages", "4297127d64ec6", "国服", "网易", "ext", "netease", "1.",
        "启动",
        "jdks"
    }.ToFrozenSet();

    static SystemInfoHelper()
    {
        try
        {
            FreeMemCounter = new PerformanceCounter("Memory", "Available MBytes", true);
            MemUsagePercentageCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", true);
            CpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true);

            // Performance Counter Pre-Heat
            FreeMemCounter.NextValue();
            MemUsagePercentageCounter.NextValue();
            CpuCounter.NextValue();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    ///     判断是否安装了 UWP 版本的 MineCraft 。
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
            StartInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")
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
        var lines = output.Split(LineChArr, StringSplitOptions.None);

        string? key = null;
        string? value = null;

        foreach (var line in lines)
            if (line.Contains(':'))
            {
                if (!string.IsNullOrEmpty(key))
                    values[key] = value!.TrimEnd();

                var parts = line.Split(SepArr, 2, StringSplitOptions.None);

                key = parts[0].Trim();
                value = parts[1].Trim();
            }
            else if (!string.IsNullOrEmpty(key))
            {
                value += line.Trim();
            }

        if (!string.IsNullOrEmpty(key))
            values[key] = value!.TrimEnd();

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
                    Dependencies = value
                        .TrimStart('{')
                        .TrimEnd('}')
                        .Split(',')
                        .Select(s => s.Trim())
                        .ToArray()
                },
                "IsPartiallyStaged" => appxPackageInfo with { IsPartiallyStaged = Convert.ToBoolean(value) },
                "SignatureKind" => appxPackageInfo with { SignatureKind = value },
                "Status" => appxPackageInfo with { Status = value },
                _ => appxPackageInfo
            };

        return appxPackageInfo;
    }

    /// <summary>
    ///     查找可能的 javaw.exe 的路径。
    /// </summary>
    /// <returns>可能的 Java 路径构成的列表。</returns>
    public static IEnumerable<string> FindJavaWindows()
    {
        try
        {
            using var rootReg = Registry.LocalMachine.OpenSubKey("SOFTWARE");

            if (rootReg == null) return [];

            using var wow64Reg = rootReg.OpenSubKey("Wow6432Node");

            var javas = FindJavaInternal(rootReg)
                .Union(FindJavaInternal(wow64Reg))
                .ToHashSet();

            var drives = DriveInfo.GetDrives();

            foreach (var drive in drives)
                FindJavaBlur(drive.RootDirectory, javas);

            FindJavaBlur(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                javas);
            FindJavaBlur(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                javas);
            FindJavaBlur(new DirectoryInfo(Environment.CurrentDirectory), javas);

            return javas;
        }
        catch
        {
            return [];
        }
    }

    static void FindJavaBlur(DirectoryInfo di, HashSet<string> javas, int maxDepth = 24)
    {
        if (!di.Exists) return;

        var stack = new Stack<(DirectoryInfo, int)>();

        stack.Push((di, 0));

        while (stack.Count > 0)
        {
            var (current, currentDepth) = stack.Pop();

            var javaPath = Path.Combine(current.FullName, "javaw.exe");

            if (File.Exists(javaPath))
            {
                javas.Add(javaPath);
                continue;
            }

            if (currentDepth >= maxDepth) continue;

            try
            {
                foreach (var subDi in current.EnumerateDirectories())
                {
                    if (subDi.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;

                    var dirName = subDi.Name.ToLowerInvariant();

                    if (dirName == "bin")
                    {
                        stack.Push((subDi, currentDepth - 1));
                        continue;
                    }

                    if (PossibleJavaDirs.Any(possibleName =>
                            dirName.Contains(possibleName, StringComparison.OrdinalIgnoreCase)))
                        stack.Push((subDi, currentDepth + 1));
                }
            }
            catch (SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (AccessViolationException)
            {
            }
        }
    }

    public static IEnumerable<string> FindJavaInternal(RegistryKey? registry)
    {
        if (registry == null) return [];

        try
        {
            using var regKey = registry.OpenSubKey("JavaSoft");
            using var javaRuntimeReg = regKey?.OpenSubKey("Java Runtime Environment");

            if (javaRuntimeReg == null)
                return [];

            var result = new List<string>();
            foreach (var ver in javaRuntimeReg.GetSubKeyNames())
            {
                var versions = javaRuntimeReg.OpenSubKey(ver);
                var javaHomes = versions?.GetValue("JavaHome");

                if (javaHomes == null) continue;

                var str = javaHomes?.ToString();

                if (string.IsNullOrWhiteSpace(str)) continue;

                result.Add($@"{str}\bin\javaw.exe");
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///     获取 系统的内存信息
    /// </summary>
    /// <returns></returns>
    public static MemoryInfo GetWindowsMemoryStatus()
    {
        if (FreeMemCounter == null) return new MemoryInfo(0, 0, 0, 0);
        if (MemUsagePercentageCounter == null) return new MemoryInfo(0, 0, 0, 0);

        try
        {
            var free = FreeMemCounter.NextValue();
            var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / Math.Pow(1024, 2);
            var used = total - free;
            var percentage = MemUsagePercentageCounter.NextValue();

            return new MemoryInfo(total, used, free, percentage);
        }
        catch (Exception)
        {
            return new MemoryInfo(0, 0, 0, 0);
        }
    }

    /// <summary>
    ///     获取系统 Cpu 信息
    /// </summary>
    /// <returns></returns>
    public static CPUInfo GetWindowsCpuUsage()
    {
        const string name = "Total %";

        if (CpuCounter == null) return new CPUInfo(0, name);

        try
        {
            var percentage = CpuCounter.NextValue();
            var val = percentage > 100 ? 100 : percentage;

            return new CPUInfo(val, name);
        }
        catch (Exception)
        {
            return new CPUInfo(0, name);
        }
    }

    public static IEnumerable<string> GetLogicalDrives()
    {
        return Environment.GetLogicalDrives();
    }

    /// <summary>
    ///     获取 Windows 系统版本，7，8.1，10，11
    /// </summary>
    /// <returns></returns>
    public static string GetWindowsMajorVersion()
    {
        return Environment.OSVersion switch
        {
            { Platform: PlatformID.Win32NT, Version: { Major: 6, Minor: 1 } } => "7",
            { Platform: PlatformID.Win32NT, Version: { Major: 6, Minor: 2 } } => "8",
            { Platform: PlatformID.Win32NT, Version: { Major: 6, Minor: 3 } } => "8.1",
            { Platform: PlatformID.Win32NT, Version: { Major: 10, Minor: 0 } } => "10",
            { Platform: PlatformID.Win32NT, Version.Build: >= 22000 } => "11",
            var os => throw new PlatformNotSupportedException($"Unknown OS version: {os}")
        };
    }

    /// <summary>
    ///     检查某个进程是否运行在 X86 模拟下
    /// </summary>
    /// <param name="proc"></param>
    /// <returns>待检查的进程，如果不传则检测当前进程</returns>
    [SupportedOSPlatform("windows10.0.10586")]
    public static unsafe bool IsRunningUnderTranslation(Process? proc = null)
    {
        if (GetWindowsMajorVersion() == "7") return false;

        proc ??= Process.GetCurrentProcess();

        var handle = proc.Handle;

        IMAGE_FILE_MACHINE processMachine;
        IMAGE_FILE_MACHINE nativeMachine;

        var result = PInvoke.IsWow64Process2(
            new HANDLE(handle),
            &processMachine,
            &nativeMachine);

        if (!result) return false;

        var nativeArch = nativeMachine switch
        {
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_AMD64 => Architecture.X64,
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_ARM64 => Architecture.Arm64,
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_ARMNT => Architecture.Arm64,
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_I386 => Architecture.X86,
            _ => throw new ArgumentException($"Unknown System Arch [{nativeMachine}]")
        };

        return nativeArch != RuntimeInformation.OSArchitecture;
    }
}