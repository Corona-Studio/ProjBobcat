using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model.Auth;

/// <summary>
///     验证结果类
/// </summary>
public class YggdrasilAuthResult : AuthResultBase
{
    /// <summary>
    ///     可用的Profiles
    /// </summary>
    public ProfileInfoModel[]? Profiles { get; set; }

    public string? LocalId { get; set; }
    public string? RemoteId { get; set; }
}