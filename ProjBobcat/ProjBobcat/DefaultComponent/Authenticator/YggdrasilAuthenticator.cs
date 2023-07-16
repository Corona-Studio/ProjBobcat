using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.JsonContexts;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Authenticator;

/// <summary>
///     表示一个正版联机凭据验证器。
/// </summary>
public class YggdrasilAuthenticator : IAuthenticator
{
    /// <summary>
    ///     Mojang官方验证服务器地址。
    /// </summary>
    const string OfficialAuthServer = "https://authserver.mojang.com";

    /// <summary>
    ///     获取或设置邮箱。
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    ///     获取或设置密码。
    /// </summary>
    public string Password { get; init; }

    /// <summary>
    ///     获取或设置验证服务器。
    ///     这个属性允许为 null 。
    /// </summary>
    public string? AuthServer { get; init; }

    /// <summary>
    ///     获取登录Api地址。
    /// </summary>
    string LoginAddress =>
        $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/authenticate";

    /// <summary>
    ///     获取令牌刷新Api地址。
    /// </summary>
    string RefreshAddress =>
        $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/refresh";

    /// <summary>
    ///     获取令牌验证Api地址。
    /// </summary>
    string ValidateAddress =>
        $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/validate";

    /// <summary>
    ///     获取令牌吊销Api地址。
    /// </summary>
    string RevokeAddress =>
        $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/invalidate";

    /// <summary>
    ///     获取登出Api地址。
    /// </summary>
    string SignOutAddress =>
        $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/signout";

    public ILauncherAccountParser LauncherAccountParser { get; set; }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <param name="userField">指示是否获取user字段。</param>
    /// <returns></returns>
    public AuthResultBase Auth(bool userField = false)
    {
        return AuthTaskAsync().Result;
    }

    /// <summary>
    ///     异步验证凭据。
    /// </summary>
    /// <param name="userField">是否获取user字段</param>
    /// <returns>验证状态。</returns>
    public async Task<AuthResultBase> AuthTaskAsync(bool userField = false)
    {
        var requestModel = new AuthRequestModel
        {
            ClientToken = LauncherAccountParser.LauncherAccount.MojangClientToken,
            RequestUser = userField,
            Username = Email,
            Password = Password
        };
        var requestJson = JsonSerializer.Serialize(requestModel, typeof(AuthRequestModel),
            new AuthRequestModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        using var resultJson = await HttpHelper.Post(LoginAddress, requestJson);

        if (!resultJson.IsSuccessStatusCode)
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "网络请求失败",
                    Error = $"验证请求返回了失败的状态码：{resultJson.StatusCode}"
                }
            };

        var result = await resultJson.Content.ReadFromJsonAsync(AuthResponseModelContext.Default.AuthResponseModel);

        if (result == default || string.IsNullOrEmpty(result.AccessToken))
        {
            var error = await resultJson.Content.ReadFromJsonAsync(ErrorModelContext.Default.ErrorModel);

            if (error is null)
                return new YggdrasilAuthResult
                {
                    AuthStatus = AuthStatus.Unknown
                };

            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = error
            };
        }

        if (result.SelectedProfile == null && !(result.AvailableProfiles?.Any() ?? false))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "没有发现档案",
                    ErrorMessage = "没有在返回消息中发现任何可用的档案",
                    Cause = "可能是因为您还没有购买正版游戏或是账户服务器出现了问题！"
                }
            };

        if (string.IsNullOrEmpty(AuthServer) && result.SelectedProfile == null)
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "没有发现档案",
                    ErrorMessage = "没有在返回消息中发现任何可用的档案",
                    Cause = "可能是因为您还没有购买正版游戏或是账户服务器出现了问题！"
                }
            };

        var profiles = result.AvailableProfiles.ToDictionary(profile => profile.UUID,
            profile => new AuthProfileModel { DisplayName = profile.Name });

        foreach (var (playerUuid, authProfileModel) in profiles)
        {
            var ids = LauncherAccountParser.LauncherAccount.Accounts.Where(a =>
                    (a.Value.MinecraftProfile?.Name?.Equals(authProfileModel.DisplayName,
                        StringComparison.OrdinalIgnoreCase) ?? false) &&
                    (a.Value.MinecraftProfile?.Id?.Equals(playerUuid.ToString(), StringComparison.OrdinalIgnoreCase) ??
                     false))
                .Select(p => p.Value.Id);

            foreach (var id in ids) LauncherAccountParser.RemoveAccount(id);
        }

        var rUuid = GuidHelper.NewGuidString();

        var profile = new AccountModel
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAt = DateTime.Now.AddHours(48),
            EligibleForMigration = false,
            HasMultipleProfiles = profiles.Count > 1,
            Legacy = false,
            LocalId = rUuid,
            Persistent = true,
            RemoteId = result.User?.UUID.ToString() ?? new Guid().ToString(),
            Type = "Mojang",
            UserProperites = result.User?.Properties?.ToAuthProperties(profiles).ToArray() ??
                             Array.Empty<AuthPropertyModel>(),
            Username = Email
        };

        if (result.SelectedProfile != null)
        {
            profile.Id = result.SelectedProfile.UUID.ToGuid();
            profile.MinecraftProfile = new AccountProfileModel
            {
                Id = result.SelectedProfile.UUID.ToString(),
                Name = result.SelectedProfile.Name
            };
        }
        /*
        else
        {
            var existsAccount = LauncherAccountParser.Find(result.SelectedProfile.UUID.ToString(), result.SelectedProfile.Name);

            if (existsAccount.HasValue)
            {
                var (_, value) = existsAccount.Value;

                if (value != null)
                {
                    if (value.MinecraftProfile != null)
                    {
                        profile.MinecraftProfile = value.MinecraftProfile;
                    }
                }
            }
        }
        */

        if (!LauncherAccountParser.AddNewAccount(rUuid, profile, out var accountId))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "添加记录时出现错误",
                    Error = "无法添加账户",
                    ErrorMessage = "请检查 launcher_accounts.json 的权限"
                }
            };

        return new YggdrasilAuthResult
        {
            Id = accountId ?? Guid.Empty,
            AccessToken = result.AccessToken,
            AuthStatus = AuthStatus.Succeeded,
            Profiles = result.AvailableProfiles,
            SelectedProfile = result.SelectedProfile,
            User = result.User,
            LocalId = rUuid,
            RemoteId = profile.RemoteId
        };
    }

    /// <summary>
    ///     获取最后一次的验证状态。
    /// </summary>
    /// <returns>验证状态。</returns>
    public AuthResultBase GetLastAuthResult()
    {
        var profile =
            LauncherAccountParser.LauncherAccount.Accounts.Values.FirstOrDefault(a =>
                a.Username.Equals(Email, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
            return new AuthResultBase
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "没有找到该账户对应的验证信息！",
                    ErrorMessage = "没有找到该账户",
                    Cause = "可能是因为该账户还没有进行过验证，凭据已被吊销或失效"
                }
            };

        if (!string.IsNullOrEmpty(profile.AccessToken))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Succeeded,
                AccessToken = profile.AccessToken,
                Profiles = new[]
                {
                    new ProfileInfoModel
                    {
                        Name = profile.Username,
                        Properties = profile.UserProperites.Select(x => new PropertyModel
                        {
                            Name = x.Name,
                            Value = x.Value
                        }).ToArray(),
                        UUID = new PlayerUUID(profile.RemoteId)
                    }
                },
                SelectedProfile = new ProfileInfoModel
                {
                    Name = profile.MinecraftProfile?.Name ?? Email,
                    UUID = new PlayerUUID()
                }
            };

        return new AuthResultBase
        {
            AuthStatus = AuthStatus.Unknown,
            Error = new ErrorModel
            {
                Error = "未知错误"
            }
        };
    }

    public async Task<AuthResultBase> AuthRefreshTaskAsync(AuthResponseModel response, bool userField = false)
    {
        var requestModel = new AuthRefreshRequestModel
        {
            AccessToken = response.AccessToken,
            ClientToken = response.ClientToken,
            RequestUser = userField,
            SelectedProfile = response.SelectedProfile
        };
        var requestJson = JsonSerializer.Serialize(requestModel, typeof(AuthRefreshRequestModel),
            new AuthRefreshRequestModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        using var resultJson = await HttpHelper.Post(RefreshAddress, requestJson);
        var resultJsonElement = await resultJson.Content.ReadFromJsonAsync(JsonElementContext.Default.JsonElement);
        object? result = resultJsonElement.TryGetProperty("cause", out _)
            ? resultJsonElement.Deserialize(ErrorModelContext.Default.ErrorModel)
            : resultJsonElement.Deserialize(AuthResponseModelContext.Default.AuthResponseModel);

        switch (result)
        {
            case ErrorModel error:
                return new AuthResultBase
                {
                    AuthStatus = AuthStatus.Failed,
                    Error = error
                };
            case AuthResponseModel authResponse:
                if (authResponse.SelectedProfile == null)
                    return new AuthResultBase
                    {
                        AuthStatus = AuthStatus.Failed,
                        Error = new ErrorModel
                        {
                            Error = "没有发现已选择的档案",
                            ErrorMessage = "没有在返回消息中发现SelectedProfile字段",
                            Cause = "可能是因为您还没有购买正版游戏！"
                        }
                    };

                var profiles = authResponse.AvailableProfiles.ToDictionary(profile => profile.UUID,
                    profile => new AuthProfileModel { DisplayName = profile.Name });

                var uuid = authResponse.User.UUID.ToString();
                var (_, value) = LauncherAccountParser.LauncherAccount.Accounts.FirstOrDefault(a =>
                    (a.Value.MinecraftProfile?.Name?.Equals(authResponse.User.UserName,
                        StringComparison.OrdinalIgnoreCase) ?? false) &&
                    (a.Value.MinecraftProfile?.Id?.Equals(uuid, StringComparison.OrdinalIgnoreCase) ?? false));

                if (value != default) LauncherAccountParser.RemoveAccount(value.Id);

                var rUuid = GuidHelper.NewGuidString();

                var profile = new AccountModel
                {
                    AccessToken = authResponse.AccessToken,
                    AccessTokenExpiresAt = DateTime.Now.AddHours(48),
                    EligibleForMigration = false,
                    HasMultipleProfiles = profiles.Count > 1,
                    Legacy = false,
                    LocalId = rUuid,
                    Persistent = true,
                    RemoteId = authResponse.User?.UUID.ToString() ?? new Guid().ToString(),
                    Type = "Mojang",
                    UserProperites = authResponse.User?.Properties?.ToAuthProperties(profiles).ToArray() ??
                                     Array.Empty<AuthPropertyModel>(),
                    Username = Email
                };

                if (authResponse.SelectedProfile != null)
                {
                    profile.Id = authResponse.SelectedProfile.UUID.ToGuid();
                    profile.MinecraftProfile = new AccountProfileModel
                    {
                        Id = authResponse.SelectedProfile.UUID.ToString(),
                        Name = authResponse.SelectedProfile.Name
                    };
                }

                if (!LauncherAccountParser.AddNewAccount(rUuid, profile, out var id))
                    return new YggdrasilAuthResult
                    {
                        AuthStatus = AuthStatus.Failed,
                        Error = new ErrorModel
                        {
                            Cause = "添加记录时出现错误",
                            Error = "无法添加账户",
                            ErrorMessage = "请检查 launcher_accounts.json 的权限"
                        }
                    };

                return new YggdrasilAuthResult
                {
                    Id = id ?? Guid.Empty,
                    AccessToken = authResponse.AccessToken,
                    AuthStatus = AuthStatus.Succeeded,
                    Profiles = authResponse.AvailableProfiles,
                    SelectedProfile = authResponse.SelectedProfile,
                    User = authResponse.User
                };
            default:
                return new AuthResultBase
                {
                    AuthStatus = AuthStatus.Unknown
                };
        }
    }

    public async Task<bool> ValidateTokenTaskAsync(string accessToken)
    {
        var requestModel = new AuthTokenRequestModel
        {
            AccessToken = accessToken,
            ClientToken = LauncherAccountParser.LauncherAccount.MojangClientToken
        };
        var requestJson = JsonSerializer.Serialize(requestModel, typeof(AuthTokenRequestModel),
            new AuthTokenRequestModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        using var result = await HttpHelper.Post(ValidateAddress, requestJson);
        return result.StatusCode.Equals(HttpStatusCode.NoContent);
    }

    public async Task TokenRevokeTaskAsync(string accessToken)
    {
        var requestModel = new AuthTokenRequestModel
        {
            AccessToken = accessToken,
            ClientToken = LauncherAccountParser.LauncherAccount.MojangClientToken
        };
        var requestJson = JsonSerializer.Serialize(requestModel, typeof(AuthTokenRequestModel),
            new AuthTokenRequestModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        using var x = await HttpHelper.Post(RevokeAddress, requestJson);
    }

    /// <summary>
    ///     登出。
    ///     返回值表示成功与否。
    /// </summary>
    /// <returns>表示成功与否。</returns>
    public async Task<bool> SignOutTaskAsync()
    {
        var requestModel = new SignOutRequestModel
        {
            Username = Email,
            Password = Password
        };
        var requestJson = JsonSerializer.Serialize(requestModel, typeof(SignOutRequestModel),
            new SignOutRequestModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        using var result = await HttpHelper.Post(SignOutAddress, requestJson);
        return result.StatusCode.Equals(HttpStatusCode.NoContent);
    }
}