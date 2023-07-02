using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Quilt;

public class QuiltSupportGameModel
{
    [JsonPropertyName("version")] public string Version { get; set; }

    [JsonPropertyName("stable")] public bool Stable { get; set; }
}

[JsonSerializable(typeof(QuiltSupportGameModel[]))]
public partial class QuiltSupportGameModelContext : JsonSerializerContext {}