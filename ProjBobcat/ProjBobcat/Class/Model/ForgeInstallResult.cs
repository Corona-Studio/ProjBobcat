using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model;

/// <summary>
///     Forge安装结果类
/// </summary>
public class ForgeInstallResult
{
    /// <summary>
    ///     指示是否成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    ///     错误信息
    /// </summary>
    public ErrorModel? Error { get; set; }
}