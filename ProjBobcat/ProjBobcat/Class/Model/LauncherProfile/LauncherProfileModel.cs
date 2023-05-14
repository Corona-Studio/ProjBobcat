using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherProfile;

public class LauncherProfileModel
{
    [JsonPropertyName("profiles")] public Dictionary<string, GameProfileModel> Profiles { get; set; }

    [JsonPropertyName("clientToken")] public string ClientToken { get; set; }

    [JsonPropertyName("selectedUser")] public SelectedUserModel SelectedUser { get; set; }

    [JsonPropertyName("launcherVersion")] public LauncherVersionModel LauncherVersion { get; set; }
}

[JsonSerializable(typeof(LauncherProfileModel))]
partial class LauncherProfileModelContext : JsonSerializerContext
{
}