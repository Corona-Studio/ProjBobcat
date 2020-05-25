using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model
{
    public class AssetFileInfo
    {
        [JsonProperty("hash")] public string Hash { get; set; }

        [JsonProperty("size")] public long Size { get; set; }
    }


    public class AssetObjectModel
    {
        [JsonProperty("objects")] public Dictionary<string, AssetFileInfo> Objects { get; set; }
    }
}