using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class
{
    public abstract class VersionLocatorBase
    {
        private protected string RootPath { get; set; }

        private protected virtual VersionInfo ToVersion(string id)
        {
            throw new NotImplementedException();
        }

        private protected virtual Tuple<List<NativeFileInfo>, List<FileInfo>> GetNatives(IEnumerable<Library> libraries)
        {
            throw new NotImplementedException();
        }

        private protected virtual Tuple<string, Dictionary<string, string>> ParseGameArguments(
            Tuple<string, List<object>> arguments)
        {
            throw new NotImplementedException();
        }

        private protected virtual RawVersionModel ParseRawVersion(string id)
        {
            throw new NotImplementedException();
        }
    }
}