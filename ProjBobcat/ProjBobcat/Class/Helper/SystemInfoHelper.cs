using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     系统信息帮助器。
/// </summary>
/// 



public static class SystemInfoHelper
{
    public static CPUInfo GetProcessorUsage()
    {
#if WINDOWS
        return Platforms.Windows.SystemInfoHelper.GetWindowsCpuUsage()
            .FirstOrDefault(i => i.Name.Equals("_Total", StringComparison.OrdinalIgnoreCase));
#elif LINUX
        return Platforms.Linux.SystemInfoHelper.GetLinuxCpuUsage().FirstOrDefault();
#else
        return null;
#endif
    }

    public static MemoryInfo GetMemoryUsage()
    {
#if WINDOWS
        return Platforms.Windows.SystemInfoHelper.GetWindowsMemoryStatus();
#elif LINUX
        return Platforms.Linux.SystemInfoHelper.GetLinuxMemoryStatus();
#else
        return null;
#endif
    }

    public static async IAsyncEnumerable<string> FindJava(bool fullSearch = false)
    {
        var result = new HashSet<string>();
        var DiskName = getDisk();

        if (fullSearch)
            await foreach (var path in DeepJavaSearcher.DeepSearch())
                result.Add(path);
        
        foreach(var JP in DiskName)
        {
            try
            {
                DirectoryInfo TheFolder = new DirectoryInfo($"{JP}\\Program Files\\Java");
                foreach (DirectoryInfo NextFolder in TheFolder.GetDirectories())
                {
                    string FullPath = JP+"\\Program Files\\Java\\" + NextFolder.Name + "\\bin\\javaw.exe";
                    if (File.Exists(FullPath))
                        result.Add(FullPath);
                }
            }
            catch { }
        }

#if WINDOWS
        foreach (var path in Platforms.Windows.SystemInfoHelper.FindJavaWindows())
            result.Add(path);
#endif

        foreach (var path in result)
            yield return path;
        foreach (var path in FindJavaInOfficialGamePath())
            yield return path;

        var evJava = FindJavaUsingEnvironmentVariable();

        if (!string.IsNullOrEmpty(evJava))
            yield return Path.Combine(evJava, "bin", "javaw.exe");
    }

    static IEnumerable<string> FindJavaInOfficialGamePath()
    {
#if WINDOWS
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#elif MACOS
        var path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        var basePath = Path.Combine(path, "Application Support");
#elif LINUX
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
#endif

        basePath = Path.Combine(basePath, ".minecraft", "runtime");

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


    public static List<string> getDisk()
    {
        WqlObjectQuery wmiquery = new WqlObjectQuery("select * from Win32_LogiCalDisk");
        ManagementObjectSearcher wmifind = new ManagementObjectSearcher(wmiquery);
        ManagementObjectCollection queryCollection = wmifind.Get();
        List<string> ls = new List<string>();
        foreach (var disk in queryCollection)
        {
            ls.Add(disk["DeviceID"].ToString());
        }
        return ls;
    }

}
