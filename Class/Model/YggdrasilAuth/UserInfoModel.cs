using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class UserInfoModel
    {
        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("username")] public string UserName { get; set; }

        [JsonProperty("properties")] public List<PropertyModel> Properties { get; set; }
    }
}