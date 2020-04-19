using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Forge
{
    /// <summary>
    ///     Install节点
    /// </summary>
    public class Install
    {
        [JsonProperty("profileName")] public string ProfileName { get; set; }

        [JsonProperty("target")] public string Target { get; set; }

        [JsonProperty("path")] public string Path { get; set; }

        [JsonProperty("version")] public string Version { get; set; }

        [JsonProperty("filePath")] public string FilePath { get; set; }

        [JsonProperty("welcome")] public string Welcome { get; set; }

        [JsonProperty("minecraft")] public string MineCraft { get; set; }

        [JsonProperty("mirrorList")] public string MirrorList { get; set; }

        [JsonProperty("logo")] public string Logo { get; set; }

        [JsonProperty("modList")] public string ModList { get; set; }
    }

    public class ForgeLibraries
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("checksums")] public List<string> CheckSums { get; set; }

        [JsonProperty("serverreq")] public bool ServerReq { get; set; }

        [JsonProperty("clientreq")] public bool ClientReq { get; set; }
    }

    public class VersionInfo
    {
        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("time")] public string Time { get; set; }

        [JsonProperty("releaseTime")] public string ReleaseTime { get; set; }

        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("minecraftArguments")] public string MinecraftArguments { get; set; }

        [JsonProperty("mainClass")] public string MainClass { get; set; }

        [JsonProperty("inheritsFrom")] public string InheritsFrom { get; set; }

        [JsonProperty("jar")] public string Jar { get; set; }

        [JsonProperty("assets")] public string Assets { get; set; }

        [JsonProperty("logging")] public object Logging { get; set; }

        [JsonProperty("libraries")] public List<ForgeLibraries> Libraries { get; set; }
    }

    public class Optional
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("client")] public bool Client { get; set; }

        [JsonProperty("server")] public bool Server { get; set; }

        [JsonProperty("inject")] public bool Inject { get; set; }

        [JsonProperty("desc")] public string Desc { get; set; }

        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("artifact")] public string Artifact { get; set; }

        [JsonProperty("maven")] public string Maven { get; set; }
    }

    /// <summary>
    ///     Forge安装文档
    /// </summary>
    public class ForgeInstallProfile
    {
        [JsonProperty("install")] public Install Install { get; set; }

        [JsonProperty("versionInfo")] public VersionInfo VersionInfo { get; set; }

        [JsonProperty("optionals")] public List<Optional> OptionalList { get; set; }
    }
}