using ProjBobcat.Class.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;

namespace ProjBobcat.Platforms.Linux
{
    class SystemInfoHelper
    {
        /// <summary>
        /// Get the system overall CPU usage percentage.
        /// </summary>
        /// <returns>The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
        /// then it will return 30.12.</returns>
        public static IEnumerable<CPUInfo> GetLinuxCpuUsage()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

            System.Threading.Thread.Sleep(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

            var cpuUsedMs = endCpuUsage - startCpuUsage;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            yield return new CPUInfo
            {
                Name = "Overrall",
                Usage = Math.Round(cpuUsageTotal * 100, 2)
            };
        }

        /// <summary>
		///     获取 系统的内存信息
		/// </summary>
		/// <returns></returns>
        public static MemoryInfo GetLinuxMemoryStatus()
        {
            var info = new ProcessStartInfo("free -m")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"free -m\"",
                RedirectStandardOutput = true
            };

            using var process = Process.Start(info);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            // Console.WriteLine(output);

            var lines = output.Split('\n');
            var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);

            var total = double.Parse(memory[1]);
            var used = double.Parse(memory[2]);
            var free = double.Parse(memory[3]);
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
