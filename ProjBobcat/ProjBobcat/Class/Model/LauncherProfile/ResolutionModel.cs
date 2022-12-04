using Newtonsoft.Json;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class ResolutionModel : IDefaultValueChecker
{
    [JsonProperty("width")] public uint Width { get; set; }

    [JsonProperty("height")] public uint Height { get; set; }

    [JsonIgnore] public bool FullScreen { get; set; }

    public bool IsDefault()
    {
        return Width == 0 && Height == 0;
    }
}