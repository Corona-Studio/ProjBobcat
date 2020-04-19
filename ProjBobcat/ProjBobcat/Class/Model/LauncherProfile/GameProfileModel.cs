using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class GameProfileModel
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("gameDir")] public string GameDir { get; set; }

        [JsonProperty("created")] public DateTime Created { get; set; }

        [JsonProperty("javaDir")] public string JavaDir { get; set; }

        [JsonProperty("resolution")] public ResolutionModel Resolution { get; set; }

        [JsonProperty("icon")] public string Icon { get; set; }

        [JsonProperty("javaArgs")] public string JavaArgs { get; set; }

        [JsonProperty("lastVersionId")] public string LastVersionId { get; set; }

        [JsonProperty("lastUsed")] public DateTime LastUsed { get; set; }

        [JsonProperty("type")] public string Type { get; set; }
    }
}