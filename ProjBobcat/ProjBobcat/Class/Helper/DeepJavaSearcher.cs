using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ProjBobcat.Class.Helper;

public static class DeepJavaSearcher
{
    static IEnumerable<string> GetLogicalDrives()
    {
        return Environment.GetLogicalDrives();
    }

    public static async IAsyncEnumerable<string> DeepSearch(string drive, string fileName)
    {
        var result = new HashSet<string>();
        var psi = new ProcessStartInfo("where")
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

        var process = Process.Start(psi);
        var isFailed = false;

        if (process == null)
            yield break;

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data)) return;

            isFailed = true;
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data)) return;
            if (File.Exists(args.Data))
                result.Add(args.Data);
        };

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync();

        if (isFailed || process.ExitCode != 0)
            yield break;

        foreach (var path in result)
        {
            yield return path;
        }
    }

    public static async IAsyncEnumerable<string> DeepSearch()
    {
        var result = new HashSet<string>();
        var drives = GetLogicalDrives();

        foreach (var drive in drives)
        {
            await foreach (var path in DeepSearch(drive, "javaw.exe"))
                yield return path;
        }
    }
}