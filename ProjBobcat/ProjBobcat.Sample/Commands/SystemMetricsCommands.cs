using Cocona;
using Microsoft.Extensions.Logging;
using ProjBobcat.Class.Helper;

namespace ProjBobcat.Sample.Commands;

public class SystemMetricsCommands(ILogger<SystemMetricsCommands> logger)
{
    [Command("cpu", Description = "Get CPU usage")]
    public async Task GetCpuUsageAsync(
        [Argument(Description = "Sample count")] int sampleCount = 6)
    {
        for (var i = 0; i < sampleCount; i++)
        {
            var cpuUsage = SystemInfoHelper.GetProcessorUsage();
            logger.LogInformation("CPU usage: {CpuUsage}", cpuUsage);
            
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    
    [Command("mem", Description = "Get memory usage")]
    public async Task GetMemoryUsageAsync(
        [Argument(Description = "Sample count")] int sampleCount = 6)
    {
        for (var i = 0; i < sampleCount; i++)
        {
            var memUsage = SystemInfoHelper.GetMemoryUsage();
            logger.LogInformation("Memory usage: {MemoryUsage}", memUsage);
            
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}