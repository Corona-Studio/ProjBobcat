using ProjBobcat.Class.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjBobcat.Platforms.MacOS
{
    class SystemInfoHelper
    {
        /// <summary>
        /// Get the system overall CPU usage percentage.
        /// </summary>
        /// <returns>The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
        /// then it will return 30.12.</returns>
        public static IEnumerable<CPUInfo> GetOSXCpuUsage()
        {
            var info = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"top -l 1 | grep -E \"^CPU\"\"",
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            using var process = Process.Start(info);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var cpu = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var userUsage = double.TryParse(cpu[2].TrimEnd('%'), out var userOut) ? userOut : 0;
            var sysUsage = double.TryParse(cpu[4].TrimEnd('%'), out var sysOut) ? sysOut : 0;
            var totalUsage = userUsage + sysUsage;

            yield return new CPUInfo
            {
                Name = "Overrall",
                Usage = totalUsage
            };
        }

        /// <summary>
		///     获取 系统的内存信息
		/// </summary>
		/// <returns></returns>
        public static MemoryInfo GetOSXMemoryStatus()
        {
            var info = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"top -l 1 -s 0 | grep PhysMem\"",
                RedirectStandardOutput = true
            };

            using var process = Process.Start(info);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            // Console.WriteLine(output);

            var memory = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var used = double.TryParse(memory[1].TrimEnd('M'), out var usedOut) ? usedOut : 0;
            var free = double.TryParse(memory[5].TrimEnd('M'), out var freeOut) ? freeOut : 0;
            var total = used + free;
            var percentage = used / total;

            var metrics = new MemoryInfo
            {
                Total = total,
                Used = used,
                Free = free,
                Percentage = percentage
            };

            return metrics;
        }
    }
}
