using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Forge
{
    public class ForgeInstallProfileData
    {
        [JsonProperty("client")] public string Client { get; set; }

        [JsonProperty("server")] public string Server { get; set; }
    }

    public class ForgeInstallProfileProcessor
    {
        [JsonProperty("sides")] public List<string> Sides { get; set; }

        [JsonProperty("jar")] public string Jar { get; set; }

        [JsonProperty("classpath")] public List<string> ClassPath { get; set; }

        [JsonProperty("args")] public List<string> Arguments { get; set; }

        [JsonProperty("outputs")] public Dictionary<string, string> Outputs { get; set; }
    }

    public class ForgeInstallProfile
    {
        [JsonProperty("_comment_")] public List<string> Comments { get; set; }

        [JsonProperty("spec")] public int Spec { get; set; }

        [JsonProperty("profile")] public string Profile { get; set; }

        [JsonProperty("version")] public string Version { get; set; }

        [JsonProperty("icon")] public string Icon { get; set; }

        [JsonProperty("json")] public string Json { get; set; }

        [JsonProperty("path")] public string Path { get; set; }

        [JsonProperty("logo")] public string Logo { get; set; }

        [JsonProperty("minecraft")] public string MineCraft { get; set; }

        [JsonProperty("welcome")] public string Welcome { get; set; }

        [JsonProperty("data")] public Dictionary<string, ForgeInstallProfileData> Data { get; set; }

        [JsonProperty("processors")] public List<ForgeInstallProfileProcessor> Processors { get; set; }

        [JsonProperty("libraries")] public List<Library> Libraries { get; set; }
    }
}