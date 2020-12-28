using System;
using System.IO;
using System.Linq;
using ProjBobcat.Class.Helper;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public static class ForgeInstallerFactory
    {
        public static bool IsLegacyForgeInstaller(string forgeExecutable)
        {
            if (string.IsNullOrEmpty(forgeExecutable))
                throw new ArgumentNullException(nameof(forgeExecutable));

            var regex = GameRegexHelper.ForgeLegacyJarRegex;
            using var archive = ArchiveFactory.Open(Path.GetFullPath(forgeExecutable));
            var flag = archive.Entries.Any(entry => !string.IsNullOrEmpty(regex.Match(entry.Key).Value));

            return flag;
        }
    }
}