using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class PlayerTextureInfoModel
{
    [JsonPropertyName("timestamp")] public long TimeStamp { get; set; }

    [JsonPropertyName("profileId")] public string ProfileId { get; set; }

    [JsonPropertyName("profileName")] public string ProfileName { get; set; }

    [JsonPropertyName("textures")] public Dictionary<string, TextureInfoModel> Textures { get; set; }
}