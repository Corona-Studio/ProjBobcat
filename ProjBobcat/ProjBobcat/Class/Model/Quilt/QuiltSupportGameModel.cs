using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Quilt;

public class QuiltSupportGameModel
{
    [JsonPropertyName("version")] public required string Version { get; init; }

    [JsonPropertyName("stable")] public bool Stable { get; set; }
}

[JsonSerializable(typeof(QuiltSupportGameModel[]))]
public partial class QuiltSupportGameModelContext : JsonSerializerContext;