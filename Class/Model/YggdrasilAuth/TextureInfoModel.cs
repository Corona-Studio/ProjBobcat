using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class TextureInfoModel
    {
        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("metadata")] public dynamic Metadata { get; set; }
    }
}