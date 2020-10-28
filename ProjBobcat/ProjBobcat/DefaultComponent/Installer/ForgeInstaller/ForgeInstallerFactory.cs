using System;
using System.IO;
using System.Linq;
using ProjBobcat.Class.Helper;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public static class ForgeInstallerFactory
    {
        public static IForgeInstaller GetForgeInstaller(string rootPath, string forgeExecutable, string javaPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentNullException(nameof(rootPath));
            if (string.IsNullOrEmpty(forgeExecutable))
                throw new ArgumentNullException(nameof(forgeExecutable));
            if (string.IsNullOrEmpty(javaPath))
                throw new ArgumentNullException(nameof(javaPath));

            var regex = GameRegexHelper.ForgeLegacyJarRegex;
            using var archive = ArchiveFactory.Open(Path.GetFullPath(forgeExecutable));
            var flag = archive.Entries.Any(entry => !string.IsNullOrEmpty(regex.Match(entry.Key).Value));

            if (flag)
                return new LegacyForgeInstaller
                {
                    ForgeExecutablePath = forgeExecutable,
                    RootPath = rootPath
                };

            return new HighVersionForgeInstaller
            {
                ForgeExecutablePath = forgeExecutable,
                JavaExecutablePath = javaPath,
                RootPath = rootPath
            };
        }
    }
}