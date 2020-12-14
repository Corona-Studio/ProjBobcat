using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherAccount
{
    public class LauncherAccountModel
    {
        [JsonProperty("accounts")] public Dictionary<string, AccountModel> Accounts { get; set; }

        [JsonProperty("activeAccountLocalId")] public string ActiveAccountLocalId { get; set; }

        [JsonProperty("mojangClientToken")] public string MojangClientToken { get; set; }
    }
}