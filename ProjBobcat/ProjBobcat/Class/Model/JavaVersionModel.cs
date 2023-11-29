using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class JavaVersionModel
{
    [JsonPropertyName("component")] public string? Component { get; set; }

    [JsonPropertyName("majorVersion")] public int MajorVersion { get; set; }
}