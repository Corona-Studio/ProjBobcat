using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Auth;

/// <summary>
///     Auth Profile类
/// </summary>
public class AuthProfileModel
{
    /// <summary>
    ///     显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; set; }
}