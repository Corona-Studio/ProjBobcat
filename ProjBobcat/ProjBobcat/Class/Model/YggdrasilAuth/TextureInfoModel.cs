using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class TextureInfoModel
{
    [JsonPropertyName("url")] public string Url { get; set; }

    [JsonPropertyName("metadata")] public JsonElement Metadata { get; set; }
}