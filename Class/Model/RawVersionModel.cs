using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    #region 文件信息（校检、下载）

    public class FileInfo
    {
        [JsonIgnore] public string Name { get; set; }
        [JsonProperty("path")] public string Path { get; set; }

        [JsonProperty("sha1")] public string Sha1 { get; set; }

        [JsonProperty("size")] public long Size { get; set; }

        [JsonProperty("url")] public string Url { get; set; }
    }

    #endregion

    #region 下载内容

    public class GameDownloadInfo
    {
        [JsonProperty("client")] public FileInfo Client { get; set; }

        [JsonProperty("server")] public FileInfo Server { get; set; }
    }

    #endregion

    #region 资源

    public class Asset
    {
        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("sha1")] public string Sha1 { get; set; }

        [JsonProperty("size")] public long Size { get; set; }

        [JsonProperty("totalSize")] public long TotalSize { get; set; }

        [JsonProperty("url")] public string Url { get; set; }
    }

    #endregion

    #region 参数

    public class Arguments
    {
        [JsonProperty("game")] public List<object> Game { get; set; }

        [JsonProperty("jvm")] public List<object> Jvm { get; set; }
    }

    #endregion

    #region 库

    public class Extract
    {
        [JsonProperty("exclude")] public List<string> Exclude { get; set; }
    }

    public class Downloads
    {
        [JsonProperty("artifact")] public FileInfo Artifact { get; set; }
        [JsonProperty("classifiers")] public Dictionary<string, FileInfo> Classifiers { get; set; }
    }

    public class Library
    {
        [JsonProperty("downloads")] public Downloads Downloads { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("extract")] public Extract Extract { get; set; }

        [JsonProperty("natives")] public Dictionary<string, string> Natives { get; set; }

        [JsonProperty("rules")] public List<JvmRules> Rules { get; set; }

        [JsonProperty("checksums")] public List<string> CheckSums { get; set; }

        [JsonProperty("serverreq")] public bool ServerRequired { get; set; }

        [JsonProperty("clientreq")] public bool ClientRequired { get; set; } = true;

        [JsonProperty("url")] public string Url { get; set; }
    }

    #endregion

    #region 日志

    public class Client
    {
        [JsonProperty("argument")] public string Argument { get; set; }

        [JsonProperty("file")] public FileInfo File { get; set; }

        [JsonProperty("type")] public string Type { get; set; }
    }

    public class Logging
    {
        [JsonProperty("client")] public Client Client { get; set; }
    }

    #endregion

    /// <summary>
    ///     版本JSON
    ///     Version's JSON Data Model
    /// </summary>
    public class RawVersionModel
    {
        /// <summary>
        ///     启动参数（老版本）
        ///     Launch arguments for the older versions
        /// </summary>
        [JsonProperty("minecraftArguments")]
        public string MinecraftArguments { get; set; }

        /// <summary>
        ///     启动参数
        ///     Launch arguments
        /// </summary>
        [JsonProperty("arguments")]
        public Arguments Arguments { get; set; }

        /// <summary>
        ///     资源信息
        /// </summary>
        [JsonProperty("assetIndex")]
        public Asset AssetIndex { get; set; }

        /// <summary>
        ///     资源版本
        /// </summary>
        [JsonProperty("assets")]
        public string AssetsVersion { get; set; }

        /// <summary>
        ///     游戏下载信息
        /// </summary>
        [JsonProperty("downloads")]
        public GameDownloadInfo Downloads { get; set; }

        /// <summary>
        ///     游戏版本
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        ///     继承于...（对于有些游戏版本如forge，其JSON配置会直接从另一个版本中继承，并且在继承基础上进行优先级更高的修改）
        ///     Inherits from...(For some game versions like forge, their JSON configuration directly inherits from another
        ///     version, based on which are be modifications with higher priority.)
        /// </summary>
        [JsonProperty("inheritsFrom")]
        public string InheritsFrom { get; set; }

        /// <summary>
        ///     库信息
        /// </summary>
        [JsonProperty("libraries")]
        public List<Library> Libraries { get; set; }

        /// <summary>
        ///     日志
        /// </summary>
        [JsonProperty("logging")]
        public Logging Logging { get; set; }

        /// <summary>
        ///     主类
        /// </summary>
        [JsonProperty("mainClass")]
        public string MainClass { get; set; }

        /// <summary>
        ///     最小启动器版本
        /// </summary>
        [JsonProperty("minimumLauncherVersion")]
        public int MinimumLauncherVersion { get; set; }

        /// <summary>
        ///     发布时间
        /// </summary>
        [JsonProperty("releaseTime")]
        public DateTime ReleaseTime { get; set; }

        /// <summary>
        ///     时间
        /// </summary>
        [JsonProperty("time")]
        public DateTime Time { get; set; }

        /// <summary>
        ///     类型
        /// </summary>
        [JsonProperty("type")]
        public string BuildType { get; set; }
    }
}