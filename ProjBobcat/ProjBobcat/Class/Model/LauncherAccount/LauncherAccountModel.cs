using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherAccount;

public class LauncherAccountModel
{
    [JsonPropertyName("accounts")] public Dictionary<string, AccountModel>? Accounts { get; set; }

    [JsonPropertyName("activeAccountLocalId")]
    public string? ActiveAccountLocalId { get; set; }

    [JsonPropertyName("mojangClientToken")]
    public required string MojangClientToken { get; init; }
}

[JsonSerializable(typeof(LauncherAccountModel))]
public partial class LauncherAccountModelContext : JsonSerializerContext;