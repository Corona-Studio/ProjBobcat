using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ProfileInfoModel
{
    [JsonProperty("id")] public PlayerUUID UUID { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("properties")] public List<PropertyModel> Properties { get; set; }
}