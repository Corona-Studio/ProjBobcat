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
    public class YggdrasilAuthenticator : IAuthenticator
    {
        /// <summary>
        ///     Mojang官方验证服务器
        /// </summary>
        private const string OfficialAuthServer = "https://authserver.mojang.com";

        /// <summary>
        ///     邮箱
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        ///     密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     验证服务器（可不填）
        /// </summary>
        public string AuthServer { get; set; }

        /// <summary>
        ///     登录api
        /// </summary>
        private string LoginAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/authenticate";

        /// <summary>
        ///     令牌刷新api
        /// </summary>
        private string RefreshAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/refresh";

        /// <summary>
        ///     令牌验证api
        /// </summary>
        private string ValidateAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/validate";

        /// <summary>
        ///     令牌吊销api
        /// </summary>
        private string RevokeAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/invalidate";

        /// <summary>
        ///     登出api
        /// </summary>
        private string SignOutAddress =>
            $"{AuthServer}{(string.IsNullOrEmpty(AuthServer) ? OfficialAuthServer : "/authserver")}/signout";

        public ILauncherProfileParser LauncherProfileParser { get; set; }

        /// <summary>
        ///     验证凭据（同步，不可用）
        /// </summary>
        /// <param name="userField"></param>
        /// <returns></returns>
        public AuthResult Auth(bool userField)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     异步验证凭据
        /// </summary>
        /// <param name="userField">是否获取user字段</param>
        /// <returns></returns>
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

            if (result.Equals(default(AuthResponseModel)))
            {
                var error = JsonConvert.DeserializeObject<ErrorModel>(content);

                if (error.Equals(default(ErrorModel)))
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

            if (result.SelectedProfile == null)
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

            var profiles = result.AvailableProfiles.ToDictionary(profile => profile.Id,
                profile => new AuthProfileModel {DisplayName = profile.Name});

            if (LauncherProfileParser.IsAuthInfoExist(profiles.First().Key, profiles.First().Value.DisplayName))
                LauncherProfileParser.RemoveAuthInfo(profiles.First().Key);

            LauncherProfileParser.AddNewAuthInfo(new AuthInfoModel
            {
                AccessToken = result.AccessToken,
                Profiles = profiles,
                Properties = AuthPropertyHelper.ToAuthProperties(result.User?.Properties, profiles),
                UserName = profiles.First().Value.DisplayName
            }, profiles.First().Key);

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
        ///     获取最后一次的验证状态
        /// </summary>
        /// <returns></returns>
        public AuthResult GetLastAuthResult()
        {
            var profile =
                LauncherProfileParser.LauncherProfile.AuthenticationDatabase.Values.FirstOrDefault(a =>
                    a.UserName.Equals(Email, StringComparison.OrdinalIgnoreCase));

            if (profile == null || profile.Equals(default))
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
                        .Select(p => new ProfileInfoModel {Name = p.Value.DisplayName, Id = p.Key}).ToList(),
                    SelectedProfile = new ProfileInfoModel
                    {
                        Name = profile.Profiles.First().Value.DisplayName,
                        Id = profile.Profiles.First().Key
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

                    var profiles = authResponse.AvailableProfiles.ToDictionary(profile => profile.Id,
                        profile => new AuthProfileModel {DisplayName = profile.Name});

                    if (LauncherProfileParser.IsAuthInfoExist(authResponse.User.Id, authResponse.User.UserName))
                        LauncherProfileParser.RemoveAuthInfo(authResponse.User.Id);

                    LauncherProfileParser.AddNewAuthInfo(new AuthInfoModel
                    {
                        AccessToken = authResponse.AccessToken,
                        Profiles = profiles,
                        Properties = new List<AuthPropertyModel>
                        {
                            new AuthPropertyModel
                            {
                                Name = authResponse.User.Properties.First().Name,
                                UserId = authResponse.User.Id,
                                Value = authResponse.User.Properties.First().Value
                            }
                        },
                        UserName = authResponse.User.UserName
                    }, authResponse.User.Id);


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