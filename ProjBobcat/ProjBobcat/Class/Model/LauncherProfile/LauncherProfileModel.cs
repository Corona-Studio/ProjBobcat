using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class LauncherProfileModel
{
    [JsonPropertyName("profiles")] public Dictionary<string, GameProfileModel>? Profiles { get; init; }

    [JsonPropertyName("clientToken")] public string? ClientToken { get; init; }

    [JsonPropertyName("selectedUser")] public SelectedUserModel? SelectedUser { get; set; }

    [JsonPropertyName("launcherVersion")] public LauncherVersionModel? LauncherVersion { get; init; }
}

[JsonSerializable(typeof(LauncherProfileModel))]
public partial class LauncherProfileModelContext : JsonSerializerContext;