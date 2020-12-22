using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    public abstract class VersionLocatorBase : LauncherParserBase, IVersionLocator
    {
        private protected abstract VersionInfo ToVersion(string id);

        public abstract Tuple<List<NativeFileInfo>, List<FileInfo>> GetNatives(
            IEnumerable<Library> libraries);

        private protected abstract Tuple<string, Dictionary<string, string>> ParseGameArguments(
            Tuple<string, List<object>> arguments);

        public abstract RawVersionModel ParseRawVersion(string id);
        
        public virtual ILauncherProfileParser LauncherProfileParser { get; set; }
        public virtual ILauncherAccountParser LauncherAccountParser { get; set; }

        public abstract VersionInfo GetGame(string id);

        public abstract IEnumerable<VersionInfo> GetAllGames();

        public abstract string ParseJvmArguments(List<object> arguments);
    }
}