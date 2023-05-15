using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class LauncherAccountModel
{
    [JsonPropertyName("accounts")] public Dictionary<string, AccountModel>? Accounts { get; set; }

    [JsonPropertyName("activeAccountLocalId")]
    public string ActiveAccountLocalId { get; set; }

    [JsonPropertyName("mojangClientToken")]
    public string MojangClientToken { get; set; }
}

[JsonSerializable(typeof(LauncherAccountModel))]
partial class LauncherAccountModelContext : JsonSerializerContext
{
}