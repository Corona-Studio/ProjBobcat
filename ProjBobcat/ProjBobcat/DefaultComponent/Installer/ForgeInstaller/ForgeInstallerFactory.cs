using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;

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
        if (string.IsNullOrEmpty(forgeExecutable))
            throw new ArgumentNullException(nameof(forgeExecutable));

        using var archive = ArchiveFactory.Open(Path.GetFullPath(forgeExecutable));

        var legacyUniversalJar =
            archive.Entries.Any(entry => entry.Key?.Equals($"forge-{forgeVersion}-universal.jar") ?? false);
        var installProfileJson = archive.Entries.Any(entry =>
            entry.Key?.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase) ?? false);

        return legacyUniversalJar && installProfileJson;
    }
}