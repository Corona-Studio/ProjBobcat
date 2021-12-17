using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class ResolutionModel
{
    [JsonProperty("width")] public uint Width { get; set; }

    [JsonProperty("height")] public uint Height { get; set; }

    [JsonIgnore] public bool FullScreen { get; set; }
}