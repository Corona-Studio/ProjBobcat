using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public static class ForgeInstallerFactory
    {
        public static bool IsLegacyForgeInstaller(string forgeExecutable, string forgeVersion)
        {
            if (string.IsNullOrEmpty(forgeExecutable))
                throw new ArgumentNullException(nameof(forgeExecutable));

            using var archive = ArchiveFactory.Open(Path.GetFullPath(forgeExecutable));

            var legacyUniversalJar =
                archive.Entries.Any(entry => entry.Key.Equals($"forge-{forgeVersion}-universal.jar"));
            var installProfileJson = archive.Entries.Any(entry =>
                entry.Key.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase));

            return legacyUniversalJar && installProfileJson;
        }
    }
}