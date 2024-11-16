using System;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model.Auth;

public class AuthResultBase
{
    public Guid Id { get; init; }

    /// <summary>
    ///     验证状态
    /// </summary>
    public AuthStatus AuthStatus { get; init; }

    /// <summary>
    ///     获取的AccessToken
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    ///     错误信息
    /// </summary>
    public ErrorModel? Error { get; init; }

    /// <summary>
    ///     选择的Profile
    /// </summary>
    public ProfileInfoModel? SelectedProfile { get; set; }

    /// <summary>
    ///     用户信息
    /// </summary>
    public UserInfoModel? User { get; init; }
}