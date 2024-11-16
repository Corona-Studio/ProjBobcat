using System;

namespace ProjBobcat.Class.Model.Auth;

public class MicrosoftAuthResult : AuthResultBase
{
    /// <summary>
    ///     皮肤
    /// </summary>
    public string? Skin { get; init; }

    /// <summary>
    ///     披风
    /// </summary>
    public string? Cape { get; init; }

    /// <summary>
    ///     刷新用 Token
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    ///     验证时间
    /// </summary>
    public DateTime CurrentAuthTime { get; init; }

    /// <summary>
    ///     XBox UID
    /// </summary>
    public string? XBoxUid { get; init; }

    /// <summary>
    ///     Token 失效时间
    /// </summary>
    public int ExpiresIn { get; init; }

    public string? Email { get; init; }
}