using System;

namespace ProjBobcat.Class.Model.Auth;

public class MicrosoftAuthResult : AuthResultBase
{
    /// <summary>
    ///     皮肤
    /// </summary>
    public string? Skin { get; set; }

    /// <summary>
    ///     刷新用 Token
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    ///     验证时间
    /// </summary>
    public DateTime CurrentAuthTime { get; set; }

    /// <summary>
    ///     XBox UID
    /// </summary>
    public string XBoxUid { get; set; }

    /// <summary>
    ///     Token 失效时间
    /// </summary>
    public int ExpiresIn { get; set; }

    public string Email { get; set; }
}