using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     系统信息帮助器。
/// </summary>
public static class SystemInfoHelper
{
    public static bool IsRunningUnderTranslation()
    {
#if WINDOWS
        return Platforms.Windows.SystemInfoHelper.IsRunningUnderTranslation();
#else
        return false;
#endif
    }

    public static CPUInfo? GetProcessorUsage()
    {
#if WINDOWS
        return Platforms.Windows.SystemInfoHelper.GetWindowsCpuUsage();
#elif OSX
        return Platforms.MacOS.SystemInfoHelper.GetOSXCpuUsage();
#elif LINUX
        return Platforms.Linux.SystemInfoHelper.GetLinuxCpuUsage();
#else
        return null;
#endif
    }

    public static MemoryInfo? GetMemoryUsage()
    {
#if WINDOWS
        return Platforms.Windows.SystemInfoHelper.GetWindowsMemoryStatus();
#elif OSX
        return Platforms.MacOS.SystemInfoHelper.GetOsxMemoryStatus();
#elif LINUX
        return Platforms.Linux.SystemInfoHelper.GetLinuxMemoryStatus();
#else
        return null;
#endif
    }

    public static async IAsyncEnumerable<string> FindJava(bool fullSearch = false)
    {
        var result = new HashSet<string>();

        if (fullSearch)
            await foreach (var path in DeepJavaSearcher.DeepSearch())
                result.Add(path);

#if WINDOWS
        foreach (var path in Platforms.Windows.SystemInfoHelper.FindJavaWindows())
            result.Add(path);
#elif OSX
        foreach (var path in Platforms.MacOS.SystemInfoHelper.FindJavaMacOS())
            result.Add(path);
#elif LINUX
        foreach(var path in Platforms.Linux.SystemInfoHelper.FindJavaLinux())
            result.Add(path);
#endif

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