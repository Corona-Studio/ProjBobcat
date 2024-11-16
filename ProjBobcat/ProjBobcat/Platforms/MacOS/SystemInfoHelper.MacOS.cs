using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Platforms.MacOS;

[SupportedOSPlatform(nameof(OSPlatform.OSX))]
static partial class SystemInfoHelper
{
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int sysctlbyname(string name, out int int_val, ref IntPtr length, IntPtr newp,
        IntPtr newlen);

    public static IEnumerable<string> FindJavaMacOS()
    {
        const string rootPath = "/Library/Java/JavaVirtualMachines";

        foreach (var dir in Directory.EnumerateDirectories(rootPath))
        {
            var filePath = $"{dir}/{Constants.JavaExecutablePath}";
            if (File.Exists(filePath))
            {
                var flag = File.GetUnixFileMode(filePath);

                if (flag.HasFlag(UnixFileMode.GroupExecute) && flag.HasFlag(UnixFileMode.UserExecute))
                    yield return filePath;
            }
        }
    }

    /// <summary>
    ///     Get the system overall CPU usage percentage.
    /// </summary>
    /// <returns>
    ///     The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
    ///     then it will return 30.12.
    /// </returns>
    public static CPUInfo GetOSXCpuUsage()
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"top -l 1 | grep -E \"^CPU\"\"",
            RedirectStandardOutput = true,
            CreateNoWindow = false
        };

        using var process = Process.Start(info);

        if (process == null)
            return new CPUInfo(-1, "Overrall");

        var output = process.StandardOutput?.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
            return new CPUInfo(-1, "Overrall");

        var cpu = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var userUsage = double.TryParse(cpu[2].TrimEnd('%'), out var userOut) ? userOut : 0;
        var sysUsage = double.TryParse(cpu[4].TrimEnd('%'), out var sysOut) ? sysOut : 0;
        var totalUsage = userUsage + sysUsage;

        return new CPUInfo(totalUsage, "Overrall");
    }

    private static ulong GetTotalMemory()
    {
        var info = new ProcessStartInfo
        {
            FileName = "/usr/sbin/sysctl",
            Arguments = "hw.memsize",
            RedirectStandardOutput = true
        };

        using var process = Process.Start(info);

        if (process == null) return 0;

        var output = process.StandardOutput?.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output)) return 0;

        var split = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var value = split.Last();

        return ulong.TryParse(value, out var outVal) ? outVal : 0;
    }

    /// <summary>
    ///     获取 系统的内存信息
    /// </summary>
    /// <returns></returns>
    public static MemoryInfo GetOsxMemoryStatus()
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"vm_stat\"",
            RedirectStandardOutput = true
        };

        using var process = Process.Start(info);

        if (process == null)
            return new MemoryInfo(-1, -1, -1, -1);

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
            return new MemoryInfo(-1, -1, -1, -1);

        var split = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var infoDic = split
            .Skip(1)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(lineSplit => (lineSplit.Take(lineSplit.Length - 1), lineSplit.Last().TrimEnd('.')))
            .Select(pair => (string.Join(' ', pair.Item1).TrimEnd(':'),
                double.TryParse(pair.Item2, out var outVal) ? outVal : 0))
            .ToDictionary(pair => pair.Item1, pair2 => pair2.Item2);

        var pageSize = uint.TryParse(NumberMatchRegex().Match(split[0]).Value, out var pageSizeOut) ? pageSizeOut : 0;
        var active = infoDic.GetValueOrDefault("Pages active", 0) * pageSize;

        var used = active / Math.Pow(1024, 2);
        var total = GetTotalMemory() / Math.Pow(1024, 2);
        var free = total - used;
        var percentage = used / total;

        return new MemoryInfo(total, used, free, percentage * 100);
    }

    public static bool IsRunningUnderTranslation()
    {
        var size = (IntPtr)sizeof(int);
        var res = sysctlbyname("sysctl.proc_translated", out var value, ref size, IntPtr.Zero, IntPtr.Zero);

        if (res != 0) return false;

        return value == 1;
    }

    [GeneratedRegex("\\d+")]
    private static partial Regex NumberMatchRegex();
}