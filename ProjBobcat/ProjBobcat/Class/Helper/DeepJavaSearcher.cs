using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ProjBobcat.Class.Helper;

public static class DeepJavaSearcher
{
    public static async IAsyncEnumerable<string> DeepSearch(string drive, string fileName)
    {
        ProcessStartInfo psi;
        if (OperatingSystem.IsWindows())
            psi = new ProcessStartInfo(Constants.WhereCommand)
            {
                ArgumentList =
                {
                    "/R",
                    $"{drive}\\",
                    fileName
                },
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
        else
            psi = new ProcessStartInfo(Constants.WhereCommand)
            {
                ArgumentList =
                {
                    "/b",
                    fileName
                },
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

        var process = Process.Start(psi);

        if (process == null)
            yield break;

        while (!process.HasExited)
        {
            // ReSharper disable once MethodHasAsyncOverload
            var line = process.StandardOutput.ReadLine();

            if (!IsValid(line)) continue;

            yield return line!;
        }

        await process.WaitForExitAsync();

        var lastLine = await process.StandardOutput.ReadLineAsync();

        if (IsValid(lastLine))
            yield return lastLine!;

        yield break;

        static bool IsValid(string? line)
        {
            return !string.IsNullOrEmpty(line) && File.Exists(line);
        }
    }

    public static async IAsyncEnumerable<string> DeepSearch()
    {
        if (OperatingSystem.IsWindows())
        {
            var drives = Platforms.Windows.SystemInfoHelper.GetLogicalDrives();

            foreach (var drive in drives)
            await foreach (var path in DeepSearch(drive, Constants.JavaExecutable))
                yield return path;
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            await foreach (var path in DeepSearch(string.Empty, Constants.JavaExecutable))
                yield return path;
    }
}