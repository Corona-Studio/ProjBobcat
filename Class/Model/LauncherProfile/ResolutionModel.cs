using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class ResolutionModel
    {
        [JsonProperty("width")] public int Width { get; set; }

        [JsonProperty("height")] public int Height { get; set; }

        [JsonIgnore] public bool FullScreen { get; set; }
    }
}