using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class PlayerTextureInfoModel
{
    [JsonPropertyName("timestamp")] public long TimeStamp { get; set; }

    [JsonPropertyName("profileId")] public required string ProfileId { get; init; }

    [JsonPropertyName("profileName")] public required string ProfileName { get; init; }

    [JsonPropertyName("textures")] public IReadOnlyDictionary<string, TextureInfoModel>? Textures { get; set; }
}