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
        return this.Width == 0 && this.Height == 0;
    }

    public override string ToString()
    {
        return $"{this.Width} * {this.Height} {(this.FullScreen ? "[F]" : string.Empty)}";
    }
}