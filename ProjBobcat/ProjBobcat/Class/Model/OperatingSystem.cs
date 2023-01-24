using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class OperatingSystem
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("version")] public string Version { get; set; }
}