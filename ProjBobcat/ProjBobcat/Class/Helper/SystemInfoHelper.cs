using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     系统信息帮助器。
/// </summary>
public static class SystemInfoHelper
{
    public static string GetSystemArch()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var arch => throw new Exception($"Unknown system arch: {arch}")
        };
    }

    [SupportedOSPlatform("windows10.0.10586")]
    [SupportedOSPlatform(nameof(OSPlatform.OSX))]
    public static bool IsRunningUnderTranslation()
    {
        if (OperatingSystem.IsWindows())
            return Platforms.Windows.SystemInfoHelper.IsRunningUnderTranslation();

        if (OperatingSystem.IsMacOS())
            return Platforms.MacOS.SystemInfoHelper.IsRunningUnderTranslation();

        return false;
    }

    public static CPUInfo? GetProcessorUsage()
    {
        if (OperatingSystem.IsWindows())
            return Platforms.Windows.SystemInfoHelper.GetWindowsCpuUsage();

        if (OperatingSystem.IsMacOS())
            return Platforms.MacOS.SystemInfoHelper.GetOSXCpuUsage();

        if (OperatingSystem.IsLinux())
            return Platforms.Linux.SystemInfoHelper.GetLinuxCpuUsage();

        return null;
    }

    public static MemoryInfo? GetMemoryUsage()
    {
        if (OperatingSystem.IsWindows())
            return Platforms.Windows.SystemInfoHelper.GetWindowsMemoryStatus();

        if (OperatingSystem.IsMacOS())
            return Platforms.MacOS.SystemInfoHelper.GetOsxMemoryStatus();

        if (OperatingSystem.IsLinux())
            return Platforms.Linux.SystemInfoHelper.GetLinuxMemoryStatus();

        return null;
    }

    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    [SupportedOSPlatform(nameof(OSPlatform.OSX))]
    [SupportedOSPlatform(nameof(OSPlatform.Linux))]
    public static async IAsyncEnumerable<string> FindJava(bool fullSearch = false)
    {
        var result = new HashSet<string>();

        if (fullSearch)
            await foreach (var path in DeepJavaSearcher.DeepSearch())
                result.Add(path);
        
        if (OperatingSystem.IsWindows())
            foreach (var path in Platforms.Windows.SystemInfoHelper.FindJavaWindows())
                result.Add(path);

        if (OperatingSystem.IsMacOS())
            foreach (var path in Platforms.MacOS.SystemInfoHelper.FindJavaMacOS())
                result.Add(path);

        if (OperatingSystem.IsLinux())
            foreach(var path in Platforms.Linux.SystemInfoHelper.FindJavaLinux())
                result.Add(path);

        foreach (var path in result)
            yield return path;
        foreach (var path in FindJavaInOfficialGamePath())
            yield return path;

        var evJava = FindJavaUsingEnvironmentVariable();

        if (!string.IsNullOrEmpty(evJava))
            yield return Path.Combine(evJava, Constants.JavaExecutablePath);
    }

    static IEnumerable<string> FindJavaInOfficialGamePath()
    {
        var basePath = Path.Combine(GamePathHelper.OfficialLauncherGamePath(), "runtime");
        var paths = new[] { "java-runtime-gamma", "java-runtime-alpha", "java-runtime-beta", "jre-legacy" };

        return paths
            .Select(path => Path.Combine(basePath, path, Constants.JavaExecutablePath))
            .Where(File.Exists);
    }

    static string? FindJavaUsingEnvironmentVariable()
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
            return null;
        }
    }
}