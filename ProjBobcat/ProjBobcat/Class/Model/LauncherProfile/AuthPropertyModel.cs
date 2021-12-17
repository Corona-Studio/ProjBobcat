using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile;

/// <summary>
///     Auth Property类
/// </summary>
public class AuthPropertyModel
{
    /// <summary>
    ///     名称
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Profile Id
    /// </summary>
    [JsonProperty("profileId")]
    public string ProfileId { get; set; }

    /// <summary>
    ///     用户Id
    /// </summary>
    [JsonProperty("userId")]
    public PlayerUUID UserId { get; set; }

    /// <summary>
    ///     值
    /// </summary>
    [JsonProperty("value")]
    public string Value { get; set; }
}