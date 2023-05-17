using System.Diagnostics;

namespace ProjBobcat.Platforms.Linux;

public static class FileHelper
{
    public static bool Chmod(string filePath, string permissions)
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            ArgumentList =
            {
                permissions,
                filePath
            },
            RedirectStandardError = true
        };

        using var process = Process.Start(info);

        if (process == null) return false;
        
        var output = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return string.IsNullOrEmpty(output);
    }
}