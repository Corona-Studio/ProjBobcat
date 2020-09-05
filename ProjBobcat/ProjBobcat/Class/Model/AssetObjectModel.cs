using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    /// <summary>
    ///     Asset文件信息类
    /// </summary>
    public class AssetFileInfo
    {
        /// <summary>
        ///     Hash检验码
        /// </summary>
        [JsonProperty("hash")]
        public string Hash { get; set; }

        /// <summary>
        ///     文件大小
        /// </summary>
        [JsonProperty("size")]
        public long Size { get; set; }
    }

    /// <summary>
    ///     Asset Object类
    /// </summary>
    public class AssetObjectModel
    {
        /// <summary>
        ///     Asset Objects集合
        /// </summary>
        [JsonProperty("objects")]
        public Dictionary<string, AssetFileInfo> Objects { get; set; }
    }
}