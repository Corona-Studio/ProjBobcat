using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class PlayerTextureInfoModel
    {
        [JsonProperty("timestamp")] public long TimeStamp { get; set; }

        [JsonProperty("profileId")] public string ProfileId { get; set; }

        [JsonProperty("profileName")] public string ProfileName { get; set; }

        [JsonProperty("textures")] public Dictionary<string, TextureInfoModel> Textures { get; set; }
    }
}