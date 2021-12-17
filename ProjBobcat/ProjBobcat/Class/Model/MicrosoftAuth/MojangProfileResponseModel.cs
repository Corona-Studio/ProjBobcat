using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class MojangSkinProfile
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("state")] public string State { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("variant")] public string Variant { get; set; }

    [JsonProperty("alias")] public string Alias { get; set; }
}

public class MojangProfileResponseModel
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("skins")] public List<MojangSkinProfile> Skins { get; set; }

    [JsonProperty("capes")] public List<object> Capes { get; set; }

    public MojangSkinProfile GetActiveSkin()
    {
        return Skins?.FirstOrDefault(x => x.State.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase));
    }
}