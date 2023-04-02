using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class MojangSkinProfile
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("state")] public string State { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }

    [JsonPropertyName("variant")] public string Variant { get; set; }

    [JsonPropertyName("alias")] public string Alias { get; set; }
}

public class MojangProfileResponseModel
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("skins")] public MojangSkinProfile[] Skins { get; set; }

    [JsonPropertyName("capes")] public JsonElement[] Capes { get; set; }

    public MojangSkinProfile? GetActiveSkin()
    {
        return Skins?.FirstOrDefault(x => x.State.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase));
    }
}

[JsonSerializable(typeof(MojangProfileResponseModel))]
partial class MojangProfileResponseModelContext : JsonSerializerContext
{
}