using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class
{
    public abstract class VersionLocatorBase
    {
        private protected string RootPath { get; set; }

        private protected abstract VersionInfo ToVersion(string id);

        private protected abstract Tuple<List<NativeFileInfo>, List<FileInfo>> GetNatives(
            IEnumerable<Library> libraries);

        private protected abstract Tuple<string, Dictionary<string, string>> ParseGameArguments(
            Tuple<string, List<object>> arguments);

        public abstract RawVersionModel ParseRawVersion(string id);
    }
}