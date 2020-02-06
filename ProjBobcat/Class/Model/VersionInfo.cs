using System.Collections.Generic;

namespace ProjBobcat.Class.Model
{
    public class VersionInfo
    {
        public string Name { get; set; }
        /// <summary>
        /// The real id of this version, like 1.14-forge-xxx
        /// </summary>
        public string Id { get; set; }
        public string MainClass { get; set; }
        public Asset AssetInfo { get; set; }
        public List<FileInfo> Libraries { get; set; }
        public List<NativeFileInfo> Natives { get; set; }
        public string JvmArguments { get; set; }
        public string GameArguments { get; set; }
        public Dictionary<string, string> AvailableGameArguments { get; set; }
        public string RootVersion { get; set; }
    }
}