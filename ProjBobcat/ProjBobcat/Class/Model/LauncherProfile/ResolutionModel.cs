using System.Text.Json.Serialization;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class ResolutionModel : IDefaultValueChecker
{
    [JsonPropertyName("width")] public uint Width { get; set; }

    [JsonPropertyName("height")] public uint Height { get; set; }

    [JsonIgnore] public bool FullScreen { get; set; }

    public bool IsDefault()
    {
        return Width == 0 && Height == 0;
    }

    public override string ToString()
    {
        return $"{Width} * {Height} {(FullScreen ? "[F]" : string.Empty)}";
    }
}