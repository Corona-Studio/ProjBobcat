using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
    public required string Email { get; init; }

    /// <summary>
    ///     获取或设置密码。
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    ///     获取或设置验证服务器。
    ///     这个属性允许为 null 。
    /// </summary>
    public string? AuthServer { get; init; }

    /// <summary>
    ///     获取登录Api地址。
    /// </summary>
    string LoginAddress =>
        $"{this.AuthServer?.TrimEnd('/')}{(string.IsNullOrEmpty(this.AuthServer) ? OfficialAuthServer : "/authserver")}/authenticate";

    /// <summary>
    ///     获取令牌刷新Api地址。
    /// </summary>
    string RefreshAddress =>
        $"{this.AuthServer?.TrimEnd('/')}{(string.IsNullOrEmpty(this.AuthServer) ? OfficialAuthServer : "/authserver")}/refresh";

    /// <summary>
    ///     获取令牌验证Api地址。
    /// </summary>
    string ValidateAddress =>
        $"{this.AuthServer?.TrimEnd('/')}{(string.IsNullOrEmpty(this.AuthServer) ? OfficialAuthServer : "/authserver")}/validate";

    /// <summary>
    ///     获取令牌吊销Api地址。
    /// </summary>
    string RevokeAddress =>
        $"{this.AuthServer?.TrimEnd('/')}{(string.IsNullOrEmpty(this.AuthServer) ? OfficialAuthServer : "/authserver")}/invalidate";

    /// <summary>
    ///     获取登出Api地址。
    /// </summary>
    string SignOutAddress =>
        $"{this.AuthServer?.TrimEnd('/')}{(string.IsNullOrEmpty(this.AuthServer) ? OfficialAuthServer : "/authserver")}/signout";

    public required IHttpClientFactory HttpClientFactory { get; init; }

    public required ILauncherAccountParser LauncherAccountParser { get; init; }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <param name="userField">指示是否获取user字段。</param>
    /// <returns></returns>
    public AuthResultBase Auth(bool userField = false)
    {
        return this.AuthTaskAsync().GetAwaiter().GetResult();
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
            ClientToken = this.LauncherAccountParser.LauncherAccount.MojangClientToken,
            RequestUser = userField,
            Username = this.Email,
            Password = this.Password
        };

        var client = this.HttpClientFactory.CreateClient();

        using var authReq = new HttpRequestMessage(HttpMethod.Post, this.LoginAddress);
        authReq.Content = JsonContent.Create(requestModel, SerializerContext.Default.AuthRequestModel);

        using var resultJson = await client.SendAsync(authReq);

        if (!resultJson.IsSuccessStatusCode)
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "The network request failed.",
                    Error = $"The authentication request returned an unsuccessful status code: {resultJson.StatusCode}."
                }
            };

        var result = await resultJson.Content.ReadFromJsonAsync(SerializerContext.Default.AuthResponseModel);

        if (result == null || string.IsNullOrEmpty(result.AccessToken))
        {
            var error = await resultJson.Content.ReadFromJsonAsync(SerializerContext.Default.ErrorModel);

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

        if (result.SelectedProfile == null &&
            (result.AvailableProfiles == null || result.AvailableProfiles.Length == 0))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "No profile found.",
                    ErrorMessage = "No available profile was found in the response.",
                    Cause = "You may not own the game, or the account server may be unavailable."
                }
            };

        if (string.IsNullOrEmpty(this.AuthServer) && result.SelectedProfile == null)
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "No profile found.",
                    ErrorMessage = "No available profile was found in the response.",
                    Cause = "You may not own the game, or the account server may be unavailable."
                }
            };

        if (result.AvailableProfiles == null || result.AvailableProfiles.Length == 0)
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "No profile found.",
                    ErrorMessage = "No available profile was found in the response.",
                    Cause = "You may not own the game, or the account server may be unavailable."
                }
            };

        var profiles =
            result.AvailableProfiles
                .ToDictionary(profile => profile.Id,
                    profile => new AuthProfileModel { DisplayName = profile.Name })
                .AsReadOnly();

        foreach (var (playerUuid, authProfileModel) in profiles)
        {
            var ids = this.LauncherAccountParser.LauncherAccount.Accounts!.Where(a =>
                    (a.Value.MinecraftProfile?.Name.Equals(authProfileModel.DisplayName,
                        StringComparison.OrdinalIgnoreCase) ?? false) &&
                    (a.Value.MinecraftProfile?.Id.Equals(playerUuid.ToString(), StringComparison.OrdinalIgnoreCase) ??
                     false))
                .Select(p => p.Value.Id);

            foreach (var id in ids) this.LauncherAccountParser.RemoveAccount(id);
        }

        var rUuid = Guid.NewGuid().ToString("N");
        var profile = new AccountModel
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAt = DateTime.Now.AddHours(48),
            EligibleForMigration = false,
            HasMultipleProfiles = profiles.Count > 1,
            Legacy = false,
            LocalId = rUuid,
            Persistent = true,
            RemoteId = result.User?.Id.ToString() ?? Guid.Empty.ToString(),
            Type = "Mojang",
            UserProperites = result.User?.Properties?.ToAuthProperties(profiles).ToArray() ?? [],
            Username = this.Email
        };

        if (result.SelectedProfile != null)
        {
            profile.Id = result.SelectedProfile.Id;
            profile.MinecraftProfile = new AccountProfileModel
            {
                Id = result.SelectedProfile.Id.ToString(),
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

        if (!this.LauncherAccountParser.AddOrReplaceAccount(rUuid, profile, out var accountId))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "An error occurred while adding the record.",
                    Error = "Failed to add the account.",
                    ErrorMessage = "Check the permissions for launcher_accounts.json."
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
        var profile = this.LauncherAccountParser.LauncherAccount.Accounts!.Values.FirstOrDefault(a =>
            a.Username.Equals(this.Email, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
            return new AuthResultBase
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "No authentication data was found for this account.",
                    ErrorMessage = "The account was not found.",
                    Cause = "The account may not have been authenticated, or its credentials may have been revoked or expired."
                }
            };

        if (!string.IsNullOrEmpty(profile.AccessToken))
            return new YggdrasilAuthResult
            {
                AuthStatus = AuthStatus.Succeeded,
                AccessToken = profile.AccessToken,
                Profiles =
                [
                    new ProfileInfoModel
                    {
                        Name = profile.Username,
                        Properties = profile.UserProperites?.Select(x => new PropertyModel
                        {
                            Name = x.Name,
                            Value = x.Value
                        }).ToArray(),
                        Id = new Guid(profile.RemoteId)
                    }
                ],
                SelectedProfile = new ProfileInfoModel
                {
                    Name = profile.MinecraftProfile?.Name ?? this.Email,
                    Id = Guid.Empty
                }
            };

        return new AuthResultBase
        {
            AuthStatus = AuthStatus.Unknown,
            Error = new ErrorModel
            {
                Error = "Unknown error."
            }
        };
    }

    public async Task<AuthResultBase> AuthForProfileTaskAsync(ProfileInfoModel? selectedProfile, bool userField = true)
    {
        var result = await AuthTaskAsync(userField);
        if (result is not YggdrasilAuthResult { AuthStatus: AuthStatus.Succeeded } authenticated) return result;

        // Assigning SelectedProfile locally does not bind an unbound token on the server.
        var selected = selectedProfile == null
            ? authenticated.SelectedProfile ?? (authenticated.Profiles?.Length == 1 ? authenticated.Profiles[0] : null)
            : authenticated.Profiles?.FirstOrDefault(p => p.Id == selectedProfile.Id);
        if (selected == null)
            return new AuthResultBase { AuthStatus = AuthStatus.Failed, Error = new ErrorModel
            { Error = "No selected profile found.", ErrorMessage = "Select an available account profile before launching." } };
        if (authenticated.SelectedProfile?.Id == selected.Id) return authenticated;
        return await AuthRefreshTaskAsync(new AuthResponseModel
        {
            AccessToken = authenticated.AccessToken,
            ClientToken = LauncherAccountParser.LauncherAccount.MojangClientToken,
            SelectedProfile = selected,
            AvailableProfiles = authenticated.Profiles,
            User = authenticated.User
        }, userField);
    }

    public async Task<AuthResultBase> AuthRefreshTaskAsync(AuthResponseModel response, bool userField = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(response.AccessToken);
        ArgumentException.ThrowIfNullOrEmpty(response.ClientToken);
        ArgumentNullException.ThrowIfNull(response.SelectedProfile);
        var requestModel = new AuthRefreshRequestModel
        {
            AccessToken = response.AccessToken,
            ClientToken = response.ClientToken,
            RequestUser = userField,
            SelectedProfile = response.SelectedProfile
        };
        var client = HttpClientFactory.CreateClient();
        using var refreshReq = new HttpRequestMessage(HttpMethod.Post, RefreshAddress);
        refreshReq.Content = JsonContent.Create(requestModel, SerializerContext.Default.AuthRefreshRequestModel);
        using var refreshRes = await client.SendAsync(refreshReq);
        if (!refreshRes.IsSuccessStatusCode)
            return new AuthResultBase { AuthStatus = AuthStatus.Failed, Error = new ErrorModel
            { Error = "Profile binding failed.", ErrorMessage = $"The authentication refresh returned {refreshRes.StatusCode}." } };
        var result = await refreshRes.Content.ReadFromJsonAsync(SerializerContext.Default.AuthResponseModel);
        if (string.IsNullOrEmpty(result?.AccessToken) || result.SelectedProfile?.Id != response.SelectedProfile.Id)
            return new AuthResultBase { AuthStatus = AuthStatus.Failed, Error = new ErrorModel
            { Error = "Invalid profile binding response.", ErrorMessage = "The refresh response did not bind the selected profile." } };

        // Refresh responses need not repeat availableProfiles or user.
        var profiles = result.AvailableProfiles ?? response.AvailableProfiles ?? [result.SelectedProfile];
        var user = result.User ?? response.User;
        var localId = Guid.NewGuid().ToString("N");
        var account = new AccountModel
        {
            Id = result.SelectedProfile.Id,
            AccessToken = result.AccessToken,
            AccessTokenExpiresAt = DateTime.Now.AddHours(48),
            HasMultipleProfiles = profiles.Length > 1,
            LocalId = localId,
            Persistent = true,
            RemoteId = user?.Id.ToString() ?? Guid.Empty.ToString(),
            Type = "Mojang",
            Username = Email,
            MinecraftProfile = new AccountProfileModel
            { Id = result.SelectedProfile.Id.ToString(), Name = result.SelectedProfile.Name }
        };
        foreach (var old in LauncherAccountParser.LauncherAccount.Accounts!.Values
                     .Where(a => a.Username.Equals(Email, StringComparison.OrdinalIgnoreCase) &&
                         a.MinecraftProfile?.Id == result.SelectedProfile.Id.ToString()).ToArray())
            LauncherAccountParser.RemoveAccount(old.Id);
        if (!LauncherAccountParser.AddOrReplaceAccount(localId, account, out var id))
            return new AuthResultBase { AuthStatus = AuthStatus.Failed, Error = new ErrorModel
            { Error = "Failed to save the refreshed account." } };
        return new YggdrasilAuthResult
        {
            Id = id ?? Guid.Empty, AccessToken = result.AccessToken, AuthStatus = AuthStatus.Succeeded,
            Profiles = profiles, SelectedProfile = result.SelectedProfile, User = user,
            LocalId = localId, RemoteId = account.RemoteId
        };
    }

    public async Task<bool> ValidateTokenTaskAsync(string accessToken)
    {
        var requestModel = new AuthTokenRequestModel
        {
            AccessToken = accessToken,
            ClientToken = this.LauncherAccountParser.LauncherAccount.MojangClientToken
        };

        var client = this.HttpClientFactory.CreateClient();

        using var validationReq = new HttpRequestMessage(HttpMethod.Post, this.ValidateAddress);
        validationReq.Content = JsonContent.Create(requestModel, SerializerContext.Default.AuthTokenRequestModel);

        using var validationRes = await client.SendAsync(validationReq);

        return validationRes.StatusCode.Equals(HttpStatusCode.NoContent);
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
            Username = this.Email,
            Password = this.Password
        };

        var client = this.HttpClientFactory.CreateClient();

        using var signoutReq = new HttpRequestMessage(HttpMethod.Post, this.SignOutAddress);
        signoutReq.Content = JsonContent.Create(requestModel, SerializerContext.Default.SignOutRequestModel);

        using var signoutRes = await client.SendAsync(signoutReq);

        return signoutRes.StatusCode.Equals(HttpStatusCode.NoContent);
    }
}
