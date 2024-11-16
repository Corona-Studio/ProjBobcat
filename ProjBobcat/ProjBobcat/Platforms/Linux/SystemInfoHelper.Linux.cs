using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Platforms.Linux;

[SupportedOSPlatform(nameof(OSPlatform.Linux))]
class SystemInfoHelper
{
    /// <summary>
    ///     Get the system overall CPU usage percentage.
    /// </summary>
    /// <returns>
    ///     The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
    ///     then it will return 30.12.
    /// </returns>
    public static CPUInfo GetLinuxCpuUsage()
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"top -bn1\"",
            RedirectStandardOutput = true
        };

        using var process = Process.Start(info);

        if (process == null)
            return new CPUInfo(-1, "Overrall");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var usage = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(arr => double.TryParse(arr[8], out var outCpu) ? outCpu : 0)
            .Sum();

        return new CPUInfo(usage / 100, "Overrall");
    }

    /// <summary>
    ///     获取 系统的内存信息
    /// </summary>
    /// <returns></returns>
    public static MemoryInfo GetLinuxMemoryStatus()
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"free -m\"",
            RedirectStandardOutput = true
        };

        using var process = Process.Start(info);

        if (process == null)
            return new MemoryInfo(-1, -1, -1, -1);

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        // Console.WriteLine(output);

        var lines = output.Split('\n');
        var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);

        var total = double.Parse(memory[1]);
        var used = double.Parse(memory[2]);
        var free = double.Parse(memory[3]);
        var percentage = used / total;

        var metrics = new MemoryInfo(total, used, free, percentage * 100);

        return metrics;
    }

    public static IEnumerable<string> FindJavaLinux()
    {
        var distribution = DistributionHelper.GetSystemDistribution();
        var jvmPath = string.Empty switch
        {
            _ when Directory.Exists("/usr/lib/jvm") => "/usr/lib/jvm",
            _ when Directory.Exists("/usr/lib64/jvm") => "/usr/lib64/jvm",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(jvmPath)) return [];

        var subJvmDirectories = Directory.GetDirectories(jvmPath);

        return distribution switch
        {
            DistributionHelper.LinuxDistribution.Arch => FindJavaArchLinux(subJvmDirectories),
            DistributionHelper.LinuxDistribution.Debian => FindJavaDebianLinux(subJvmDirectories),
            DistributionHelper.LinuxDistribution.RedHat => FindJavaRedHatLinux(subJvmDirectories),
            DistributionHelper.LinuxDistribution.OpenSuse => FindJavaSuseLinux(subJvmDirectories),
            _ => FindJavaOtherLinux()
        };
    }

    static IEnumerable<string> FindJavaArchLinux(string[] paths)
    {
        return
            from path in paths
            where !path.Contains("default", StringComparison.OrdinalIgnoreCase)
            select $"{path}/bin/java"
            into result
            where File.Exists(result)
            select result;
    }

    static IEnumerable<string> FindJavaDebianLinux(string[] paths)
    {
        return
            from path in paths
            select $"{path}/bin/java"
            into result
            where File.Exists(result)
            select result;
    }

    static IEnumerable<string> FindJavaRedHatLinux(string[] paths)
    {
        return
            from path in paths
            where !path.Contains("jre", StringComparison.OrdinalIgnoreCase)
            select $"{path}/bin/java"
            into result
            where File.Exists(result)
            select result;
    }

    static IEnumerable<string> FindJavaSuseLinux(string[] paths)
    {
        return
            from path in paths
            where !path.Contains("jre", StringComparison.OrdinalIgnoreCase)
            select $"{path}/bin/java"
            into result
            where File.Exists(result)
            select result;
    }

    static IEnumerable<string> FindJavaOtherLinux()
    {
        var jvmPath = string.Empty switch
        {
            _ when Directory.Exists("/usr/lib/jvm") => "/usr/lib/jvm",
            _ when Directory.Exists("/usr/lib32/jvm") => "/usr/lib32/jvm",
            _ => "/usr/lib64/jvm"
        };

        foreach (var path in Directory.GetDirectories(jvmPath))
        {
            var result = $"{path}/bin/java";

            if (File.Exists(result)) yield return result;
        }
    }
}