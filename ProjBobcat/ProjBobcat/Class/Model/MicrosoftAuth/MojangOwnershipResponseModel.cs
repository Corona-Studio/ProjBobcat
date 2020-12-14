using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class OwnershipItem
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("signature")] public string Signature { get; set; }
    }

    public class MojangOwnershipResponseModel
    {
        [JsonProperty("items")] public List<OwnershipItem> Items { get; set; }

        [JsonProperty("signature")] public string Signature { get; set; }

        [JsonProperty("keyId")] public string KeyId { get; set; }
    }
}