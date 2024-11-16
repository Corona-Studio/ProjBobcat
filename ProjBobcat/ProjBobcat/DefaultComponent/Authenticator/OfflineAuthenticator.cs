using System;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Authenticator;

/// <summary>
///     表示一个离线凭据验证器。
/// </summary>
public class OfflineAuthenticator : IAuthenticator
{
    /// <summary>
    ///     获取或设置用户名。
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    ///     获取或设置启动程序配置文件分析器。
    /// </summary>
    public required ILauncherAccountParser LauncherAccountParser { get; init; }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <param name="userField">该参数将被忽略。</param>
    /// <returns>身份验证结果。</returns>
    public AuthResultBase Auth(bool userField = false)
    {
        var authProperty = new AuthPropertyModel
        {
            Name = "preferredLanguage",
            ProfileId = string.Empty,
            UserId = PlayerUUID.Random(),
            Value = "zh-cn"
        };

        var uuid = PlayerUUID.FromOfflinePlayerName(this.Username);

        var localUuid = Guid.NewGuid().ToString("N");
        var accountModel = new AccountModel
        {
            Id = uuid.ToGuid(),
            AccessToken = Guid.NewGuid().ToString("N"),
            AccessTokenExpiresAt = DateTime.Now,
            EligibleForMigration = false,
            HasMultipleProfiles = false,
            Legacy = false,
            LocalId = localUuid,
            MinecraftProfile = new AccountProfileModel
            {
                Id = uuid.ToString(),
                Name = this.Username
            },
            Persistent = true,
            RemoteId = Guid.NewGuid().ToString("N"),
            Type = "Mojang",
            UserProperites = [authProperty],
            Username = this.Username
        };

        if (!this.LauncherAccountParser.AddNewAccount(localUuid, accountModel, out var id))
            return new AuthResultBase
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "添加记录时出现错误",
                    Error = "无法添加账户",
                    ErrorMessage = "请检查 launcher_accounts.json 的权限"
                }
            };

        var result = new AuthResultBase
        {
            Id = id ?? Guid.Empty,
            AccessToken = Guid.NewGuid().ToString("N"),
            AuthStatus = AuthStatus.Succeeded,
            SelectedProfile = new ProfileInfoModel
            {
                Name = this.Username,
                UUID = uuid
            },
            User = new UserInfoModel
            {
                UUID = uuid,
                Properties =
                [
                    new PropertyModel
                    {
                        Name = authProperty.Name,
                        Value = authProperty.Value
                    }
                ]
            }
        };

        return result;
    }

    /// <summary>
    ///     异步验证凭据。
    /// </summary>
    /// <param name="userField">改参数将被忽略。</param>
    /// <returns></returns>
    public Task<AuthResultBase> AuthTaskAsync(bool userField)
    {
        return Task.FromResult(this.Auth());
    }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <returns>验证结果。</returns>
    [Obsolete("此方法已过时，请使用 Auth(bool) 代替。")]
    public AuthResultBase GetLastAuthResult()
    {
        return this.Auth();
    }
}