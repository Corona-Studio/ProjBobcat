using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.Authenticator
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

        public ILauncherProfileParser LauncherProfileParser { get; set; }

        /// <summary>
        ///     验证凭据。
        /// </summary>
        /// <param name="userField">指示是否获取user字段。</param>
        /// <returns></returns>
        public AuthResult Auth(bool userField = false)
        {
            return AuthTaskAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        ///     异步验证凭据。
        /// </summary>
        /// <param name="userField">是否获取user字段</param>
        /// <returns>验证状态。</returns>
        public async Task<AuthResult> AuthTaskAsync(bool userField = false)
        {
            var requestModel = new AuthRequestModel
            {
                ClientToken = LauncherProfileParser.LauncherProfile.ClientToken,
                RequestUser = userField,
                Username = Email,
                Password = Password
            };
            var requestJson = JsonConvert.SerializeObject(requestModel, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            var resultJson = await HttpHelper.Post(LoginAddress, requestJson).ConfigureAwait(true);
            var content = await resultJson.Content.ReadAsStringAsync().ConfigureAwait(true);
            var result = JsonConvert.DeserializeObject<AuthResponseModel>(content);

            if (result is null)
            {
                var error = JsonConvert.DeserializeObject<ErrorModel>(content);

                if (error is null)
                    return new AuthResult
                    {
                        AuthStatus = AuthStatus.Unknown
                    };

                return new AuthResult
                {
                    AuthStatus = AuthStatus.Failed,
                    Error = error
                };
            }

            if (result.SelectedProfile == null && !(result.AvailableProfiles?.Any() ?? false))
                return new AuthResult
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

            foreach (var kv in profiles.Where(kv =>
                LauncherProfileParser.IsAuthInfoExist(kv.Key, kv.Value.DisplayName)))
                LauncherProfileParser.RemoveAuthInfo(kv.Key);

            LauncherProfileParser.AddNewAuthInfo(new AuthInfoModel
            {
                AccessToken = result.AccessToken,
                Profiles = profiles,
                Properties = (result.User?.Properties).ToAuthProperties(profiles).ToList(),
                UserName = profiles.First().Value.DisplayName
            }, result.User!.UUID);

            return new AuthResult
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
        public AuthResult GetLastAuthResult()
        {
            var profile =
                LauncherProfileParser.LauncherProfile.AuthenticationDatabase.Values.FirstOrDefault(a =>
                    a.UserName.Equals(Email, StringComparison.OrdinalIgnoreCase));

            if (profile is null)
                return new AuthResult
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
                return new AuthResult
                {
                    AuthStatus = AuthStatus.Succeeded,
                    AccessToken = profile.AccessToken,
                    Profiles = profile.Profiles
                        .Select(p => new ProfileInfoModel {Name = p.Value.DisplayName, UUID = p.Key}).ToList(),
                    SelectedProfile = new ProfileInfoModel
                    {
                        Name = profile.Profiles.First().Value.DisplayName,
                        UUID = profile.Profiles.First().Key
                    }
                };

            return new AuthResult
            {
                AuthStatus = AuthStatus.Unknown,
                Error = new ErrorModel
                {
                    Error = "未知错误"
                }
            };
        }

        public async Task<AuthResult> AuthRefreshTaskAsync(AuthResponseModel response, bool userField = false)
        {
            var requestModel = new AuthRefreshRequestModel
            {
                AccessToken = response.AccessToken,
                ClientToken = response.ClientToken,
                RequestUser = userField,
                SelectedProfile = response.SelectedProfile
            };
            var requestJson = JsonConvert.SerializeObject(requestModel, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            var resultJson = await HttpHelper.Post(RefreshAddress, requestJson).ConfigureAwait(true);
            var content = await resultJson.Content.ReadAsStringAsync().ConfigureAwait(true);
            var result = JsonConvert.DeserializeObject<object>(content);

            switch (result)
            {
                case ErrorModel error:
                    return new AuthResult
                    {
                        AuthStatus = AuthStatus.Failed,
                        Error = error
                    };
                case AuthResponseModel authResponse:
                    if (authResponse.SelectedProfile == null)
                        return new AuthResult
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

                    var uuid = authResponse.User.UUID;
                    if (LauncherProfileParser.IsAuthInfoExist(uuid, authResponse.User.UserName))
                        LauncherProfileParser.RemoveAuthInfo(uuid);

                    LauncherProfileParser.AddNewAuthInfo(new AuthInfoModel
                    {
                        AccessToken = authResponse.AccessToken,
                        Profiles = profiles,
                        Properties = new List<AuthPropertyModel>
                        {
                            new AuthPropertyModel
                            {
                                Name = authResponse.User.Properties.First().Name,
                                UserId = authResponse.User.UUID,
                                Value = authResponse.User.Properties.First().Value
                            }
                        },
                        UserName = authResponse.User.UserName
                    }, uuid);


                    return new AuthResult
                    {
                        AccessToken = authResponse.AccessToken,
                        AuthStatus = AuthStatus.Succeeded,
                        Profiles = authResponse.AvailableProfiles,
                        SelectedProfile = authResponse.SelectedProfile,
                        User = authResponse.User
                    };
                default:
                    return new AuthResult
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
                ClientToken = LauncherProfileParser.LauncherProfile.ClientToken
            };
            var requestJson = JsonConvert.SerializeObject(requestModel, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            var result = await HttpHelper.Post(ValidateAddress, requestJson).ConfigureAwait(true);
            return result.StatusCode.Equals(HttpStatusCode.NoContent);
        }

        public async Task TokenRevokeTaskAsync(string accessToken)
        {
            var requestModel = new AuthTokenRequestModel
            {
                AccessToken = accessToken,
                ClientToken = LauncherProfileParser.LauncherProfile.ClientToken
            };
            var requestJson = JsonConvert.SerializeObject(requestModel, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            _ = await HttpHelper.Post(RevokeAddress, requestJson).ConfigureAwait(true);
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
            var requestJson = JsonConvert.SerializeObject(requestModel, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            var result = await HttpHelper.Post(SignOutAddress, requestJson).ConfigureAwait(true);
            return result.StatusCode.Equals(HttpStatusCode.NoContent);
        }
    }
}