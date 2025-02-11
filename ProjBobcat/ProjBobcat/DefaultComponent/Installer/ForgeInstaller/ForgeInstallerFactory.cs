using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller;

public static class ForgeInstallerFactory
{
    public static string GetForgeArtifactVersion(string mcVersion, string forgeVersion)
    {
        var mcVer = new Version(mcVersion);

        return (mcVer.Minor, mcVer.Build) switch
        {
            (8, 8 or -1) => $"{mcVersion}-{forgeVersion}", //1.8.8, 1.8
            (>= 7 and <= 8, _) => $"{mcVersion}-{forgeVersion}-{mcVersion}", //1.7 - 1.8, 1.8.9
            _ => $"{mcVersion}-{forgeVersion}" //1.8.9+
        };
    }

    public static bool IsLegacyForgeInstaller(string forgeExecutable, string forgeVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(forgeExecutable);

        var forgeExecutableFullPath = Path.GetFullPath(forgeExecutable);

        using var forgeExecutableFs = File.OpenRead(forgeExecutableFullPath);
        using var archive = new ZipArchive(forgeExecutableFs, ZipArchiveMode.Read);

        var legacyUniversalJar =
            archive.Entries.Any(entry => entry.FullName.Equals($"forge-{forgeVersion}-universal.jar"));
        var installProfileJson = archive.Entries.Any(entry =>
            entry.FullName.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase));

        return legacyUniversalJar && installProfileJson;
    }
}