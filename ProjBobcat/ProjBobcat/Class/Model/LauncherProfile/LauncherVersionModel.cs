using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class LauncherVersionModel
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("format")] public int Format { get; set; }
}