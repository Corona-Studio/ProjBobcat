using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LiteLoader
{
    public class LiteLoaderBuildModel
    {
        [JsonProperty("tweakClass")] public string TweakClass { get; set; }
        [JsonProperty("libraries")] public List<Library> Libraries { get; set; }
        [JsonProperty("stream")] public string Stream { get; set; }
        [JsonProperty("file")] public string File { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("md5")] public string Md5 { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
        [JsonProperty("srcJar")] public string SrcJar { get; set; }
        [JsonProperty("mcpJar")] public string McpJar { get; set; }
    }
}