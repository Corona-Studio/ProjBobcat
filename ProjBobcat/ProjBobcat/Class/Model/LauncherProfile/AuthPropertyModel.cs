using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.LauncherProfile;

/// <summary>
///     Auth Property类
/// </summary>
public class AuthPropertyModel
{
    /// <summary>
    ///     名称
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    ///     Profile Id
    /// </summary>
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; set; }

    /// <summary>
    ///     用户Id
    /// </summary>
    [JsonPropertyName("userId")]
    public PlayerUUID UserId { get; set; }

    /// <summary>
    ///     值
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; set; }
}