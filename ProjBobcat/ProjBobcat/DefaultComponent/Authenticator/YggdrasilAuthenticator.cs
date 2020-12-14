using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Authenticator
{
    /// <summary>
    ///     表示一个正版联机凭据验证器。
    /// </summary>
    public class YggdrasilAuthenticator : IAuthenticator
    {
        /// <summary>
        ///     Mojang官方验证服务器地址。
        /// </summary>
        private const string OfficialAuthServer = "https://authserver.mojang.com";

        /// <summary>
        ///     获取或设置邮箱。
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        ///     获取或设置密码。
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     获取或设置验证服务器。
        ///     这个属性允许为 null 。
        /// </summary>
        public string AuthServer { get; set; }

        /// <summary>
        ///     获取登录Api地址。
        /// </summary>
        private string LoginAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/authenticate";

        /// <summary>
        ///     获取令牌刷新Api地址。
        /// </summary>
        private string RefreshAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/refresh";

        /// <summary>
        ///     获取令牌验证Api地址。
        /// </summary>
        private string ValidateAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/validate";

        /// <summary>
        ///     获取令牌吊销Api地址。
        /// </summary>
        private string RevokeAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/invalidate";

        /// <summary>
        ///     获取登出Api地址。
        /// </summary>
        private string SignOutAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/signout";

        public ILauncherAccountParser LauncherAccountParser { get; set; }

        /// <summary>
        ///     验证凭据。
        /// </summary>
        /// <param name="userField">指示是否获取user字段。</param>
        /// <returns></returns>
        public AuthResultBase Auth(bool userField = false)
        {
            return AuthTaskAsync().GetAwaiter().GetResult();
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
            var requestJson = JsonConvert.SerializeObject(requestModel, JsonHelper.CamelCasePropertyNamesSettings);

            using var resultJson = await HttpHelper.Post(LoginAddress, requestJson).ConfigureAwait(true);
            var content = await resultJson.Content.ReadAsStringAsync().ConfigureAwait(true);
            var result = JsonConvert.DeserializeObject<AuthResponseModel>(content);

            if (result is null)
            {
                var error = JsonConvert.DeserializeObject<ErrorModel>(content);

                if (error is null)
                    return new AuthResultBase
                    {
                        AuthStatus = AuthStatus.Unknown
                    };

                return new AuthResultBase
                {
                    AuthStatus = AuthStatus.Failed,
                    Error = error
                };
            }

            if (result.SelectedProfile == null && !(result.AvailableProfiles?.Any() ?? false))
                return new AuthResultBase
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
                return new AuthResultBase
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
                profile => new AuthProfileModel {DisplayName = profile.Name});

            foreach (var (playerUuid, authProfileModel) in profiles)
                LauncherAccountParser.RemoveAccount(playerUuid.ToString(), authProfileModel.DisplayName);

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
                RemoteId = result.User.UUID.ToString(),
                Type = "Mojang",
                UserProperites = (result.User?.Properties).ToAuthProperties(profiles).ToList(),
                Username = Email
            };

            if (result.SelectedProfile != null)
                profile.MinecraftProfile = new AccountProfileModel
                {
                    Id = result.SelectedProfile.UUID.ToString(),
                    Name = result.SelectedProfile.Name
                };

            LauncherAccountParser.AddNewAccount(rUuid, profile);

            return new YggdrasilAuthResult
            {
                AccessToken = result.AccessToken,
                AuthStatus = AuthStatus.Succeeded,
                Profiles = result.AvailableProfiles,
                SelectedProfile = result.SelectedProfile,
                User = result.User
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
                    Profiles = new List<ProfileInfoModel>
                    {
                        new ProfileInfoModel
                        {
                            Name = profile.Username,
                            Properties = profile.UserProperites.Select(x => new PropertyModel
                            {
                                Name = x.Name,
                                Value = x.Value
                            }).ToList(),
                            UUID = new PlayerUUID(profile.RemoteId)
                        }
                    },
                    SelectedProfile = new ProfileInfoModel
                    {
                        Name = profile.MinecraftProfile.Name,
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
            var requestJson = JsonConvert.SerializeObject(requestModel, JsonHelper.CamelCasePropertyNamesSettings);

            using var resultJson = await HttpHelper.Post(RefreshAddress, requestJson).ConfigureAwait(true);
            var content = await resultJson.Content.ReadAsStringAsync().ConfigureAwait(true);
            var result = JsonConvert.DeserializeObject<object>(content);

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
                        profile => new AuthProfileModel {DisplayName = profile.Name});

                    var uuid = authResponse.User.UUID.ToString();
                    LauncherAccountParser.RemoveAccount(uuid, authResponse.User.UserName);

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
                        RemoteId = authResponse.User.UUID.ToString(),
                        Type = "Mojang",
                        UserProperites = (authResponse.User?.Properties).ToAuthProperties(profiles).ToList(),
                        Username = Email
                    };

                    if (authResponse.SelectedProfile != null)
                        profile.MinecraftProfile = new AccountProfileModel
                        {
                            Id = authResponse.SelectedProfile.UUID.ToString(),
                            Name = authResponse.SelectedProfile.Name
                        };

                    LauncherAccountParser.AddNewAccount(rUuid, profile);


                    return new YggdrasilAuthResult
                    {
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
            var requestJson = JsonConvert.SerializeObject(requestModel, JsonHelper.CamelCasePropertyNamesSettings);

            using var result = await HttpHelper.Post(ValidateAddress, requestJson).ConfigureAwait(true);
            return result.StatusCode.Equals(HttpStatusCode.NoContent);
        }

        public async Task TokenRevokeTaskAsync(string accessToken)
        {
            var requestModel = new AuthTokenRequestModel
            {
                AccessToken = accessToken,
                ClientToken = LauncherAccountParser.LauncherAccount.MojangClientToken
            };
            var requestJson = JsonConvert.SerializeObject(requestModel, JsonHelper.CamelCasePropertyNamesSettings);

            using var x = await HttpHelper.Post(RevokeAddress, requestJson).ConfigureAwait(true);
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
            var requestJson = JsonConvert.SerializeObject(requestModel, JsonHelper.CamelCasePropertyNamesSettings);

            using var result = await HttpHelper.Post(SignOutAddress, requestJson).ConfigureAwait(true);
            return result.StatusCode.Equals(HttpStatusCode.NoContent);
        }
    }
}