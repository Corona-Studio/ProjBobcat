using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Cocona;
using Microsoft.Extensions.Logging;
using ProjBobcat.Class.Helper;

namespace ProjBobcat.Sample.Commands;

public class SystemInfoCommands(ILogger<SystemInfoCommands> logger)
{
    [SupportedOSPlatform("windows10.0.10586")]
    [SupportedOSPlatform(nameof(OSPlatform.OSX))]
    [Command("running-native", Description = "Check if the current process is running on native platform.")]
    public void CheckCpuTranslation()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogError("This command is not supported on Linux.");
            return;
        }
        
        logger.LogInformation("Is running under translation: {IsRunningUnderTranslation}",
            SystemInfoHelper.IsRunningUnderTranslation());
    }
    
    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    [SupportedOSPlatform(nameof(OSPlatform.OSX))]
    [SupportedOSPlatform(nameof(OSPlatform.Linux))]
    [Command("find-java", Description = "Search Java on the local machine")]
    public async Task SearchJavaAsync(
        [Option("d", Description = "Enable deep search")] bool deepSearch)
    {
        logger.LogInformation("Searching Java...");
        
        await foreach(var java in SystemInfoHelper.FindJava(deepSearch))
            logger.LogInformation("Found Java: {JavaPath}", java);
    }
    
    [Command("arch", Description = "Get system arch")]
    public void CheckSystemArch()
    {
        logger.LogInformation("System arch: {SystemArch}", SystemInfoHelper.GetSystemArch());
    }
}