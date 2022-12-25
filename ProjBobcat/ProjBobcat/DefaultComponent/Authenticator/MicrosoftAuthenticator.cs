using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.Microsoft.Graph;
using ProjBobcat.Class.Model.MicrosoftAuth;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Authenticator;

public class MicrosoftAuthenticator : IAuthenticator
{
    public const string MSAuthScope = "XboxLive.signin offline_access";
    public const string MSAuthXBLUrl = "https://user.auth.xboxlive.com/user/authenticate";
    public const string MSAuthXSTSUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    public const string MojangAuthUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    public const string MojangOwnershipUrl = "https://api.minecraftservices.com/entitlements/mcstore";
    public const string MojangProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
    public const string MSGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    public MicrosoftAuthenticator()
    {
        if (ApiSettings == null)
            throw new ArgumentNullException(
                "请使用 Configure(MicrosoftAuthenticatorAPISettings apiSettings) 方法来配置验证器基础设置！");
    }

    public static MicrosoftAuthenticatorAPISettings ApiSettings { get; private set; }

    public static string MSDeviceTokenRequestUrl =>
        $"https://login.microsoftonline.com/{ApiSettings.TenentId}/oauth2/v2.0/devicecode";

    public static string MSDeviceTokenStatusUrl =>
        $"https://login.microsoftonline.com/{ApiSettings.TenentId}/oauth2/v2.0/token";

    public static string MSRefreshTokenRequestUrl =>
        $"https://login.microsoftonline.com/{ApiSettings.TenentId}/oauth2/v2.0/token";

    static HttpClient DefaultClient => HttpClientHelper.GetNewClient(HttpClientHelper.DefaultClientName);

    public string Email { get; set; }
    public Func<Task<(bool, GraphAuthResultModel)>> CacheTokenProvider { get; init; }
    public ILauncherAccountParser LauncherAccountParser { get; set; }

    public AuthResultBase Auth(bool userField = false)
    {
        return AuthTaskAsync(userField).Result;
    }

    public async Task<AuthResultBase> AuthTaskAsync(bool userField = false)
    {
        if (CacheTokenProvider == null)
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "缺少重要凭据",
                    Error = "未提供有效的数据",
                    ErrorMessage = "缺失重要的登陆参数"
                }
            };

        var (isCredentialValid, cacheAuthResult) = await CacheTokenProvider();

        if (!isCredentialValid)
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "无法获取认证 Token",
                    Error = "XBox Live 验证失败",
                    ErrorMessage = "XBox Live 验证失败"
                }
            };

        var accessToken = cacheAuthResult.AccessToken;
        var refreshToken = cacheAuthResult.RefreshToken;
        var idToken = cacheAuthResult.IdToken;
        var expiresIn = cacheAuthResult.ExpiresIn;

        #region STAGE 1

        var xBoxLiveToken =
            await SendRequest<AuthXSTSResponseModel>(MSAuthXBLUrl, AuthXBLRequestModel.Get(accessToken));

        if (xBoxLiveToken == null)
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "无法获取认证 Token",
                    Error = "XBox Live 验证失败",
                    ErrorMessage = "XBox Live 验证失败"
                }
            };

        #endregion

        #region STAGE 2

        var xStsReqStr = JsonConvert.SerializeObject(AuthXSTSRequestModel.Get(xBoxLiveToken.Token));
        using var xStsMessage = await HttpHelper.Post(MSAuthXSTSUrl, xStsReqStr);

        if (!xStsMessage.IsSuccessStatusCode)
        {
            var errContent = await xStsMessage.Content.ReadAsStringAsync();
            var errModel = JsonConvert.DeserializeObject<AuthXSTSErrorModel>(errContent);
            var reason = (errModel?.XErr ?? 0) switch
            {
                2148916233 => "未创建 XBox 账户",
                2148916238 => "未成年人账户",
                _ => "未知"
            };

            var err = new ErrorModel
            {
                Cause = reason,
                Error = $"XSTS 认证失败，原因：{reason}",
                ErrorMessage = errModel?.Message ?? "未知"
            };

            if (!string.IsNullOrEmpty(errModel?.Redirect)) err.Error += $"，相关链接：{errModel.Redirect}";

            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = err
            };
        }

        var xStsResStr = await xStsMessage.Content.ReadAsStringAsync();
        var xStsRes = JsonConvert.DeserializeObject<AuthXSTSResponseModel>(xStsResStr);

        #endregion

        #region STAGE 3

        var mcReqModel = new
        {
            identityToken = $"XBL3.0 x={xStsRes.DisplayClaims["xui"].First()["uhs"]};{xStsRes.Token}"
        };
        var mcRes = await SendRequest<AuthMojangResponseModel>(MojangAuthUrl, mcReqModel);

        #endregion

        #region STAGE 4

        using var ownResRes = await HttpHelper.Get(MojangOwnershipUrl,
            new Tuple<string, string>("Bearer", mcRes.AccessToken));
        var ownResStr = await ownResRes.Content.ReadAsStringAsync();
        var ownRes = JsonConvert.DeserializeObject<MojangOwnershipResponseModel>(ownResStr);

        if (!(ownRes?.Items?.Any() ?? false))
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "您没有购买游戏",
                    ErrorMessage = "该账户没有找到 MineCraft 正版拷贝，登录终止",
                    Cause = "没有购买游戏"
                }
            };

        #endregion

        #region STAGE 5

        using var profileResRes =
            await HttpHelper.Get(MojangProfileUrl, new Tuple<string, string>("Bearer", mcRes.AccessToken));
        var profileResStr = await profileResRes.Content.ReadAsStringAsync();
        var profileRes = JsonConvert.DeserializeObject<MojangProfileResponseModel>(profileResStr);

        if (profileRes == null)
        {
            var errModel = JsonConvert.DeserializeObject<MojangErrorResponseModel>(profileResStr);

            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "没有找到账户档案",
                    Error = $"无法从服务器拉取用户档案，原因：{errModel?.Error ?? "未知"}",
                    ErrorMessage = errModel?.ErrorMessage ?? "未知"
                }
            };
        }

        #endregion

        var uuid = Guid.NewGuid().ToString("N");
        var accountModel = new AccountModel
        {
            AccessToken = mcRes.AccessToken,
            AccessTokenExpiresAt = DateTime.Now.AddSeconds(expiresIn > 0 ? expiresIn : mcRes.ExpiresIn),
            Avatar = profileRes.GetActiveSkin()?.Url,
            EligibleForMigration = false,
            HasMultipleProfiles = false,
            Legacy = false,
            LocalId = uuid,
            MinecraftProfile = new AccountProfileModel
            {
                Id = profileRes.Id,
                Name = profileRes.Name
            },
            Persistent = true,
            RemoteId = profileRes.Name,
            Type = "XBox",
            UserProperites = new List<AuthPropertyModel>(),
            Username = profileRes.Name
        };

        if (!LauncherAccountParser.AddNewAccount(uuid, accountModel, out var id))
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "添加记录时出现错误",
                    Error = "无法添加账户",
                    ErrorMessage = "请检查 launcher_accounts.json 的权限"
                }
            };

        var sPUuid = new PlayerUUID(profileRes.Id);
        var sP = new ProfileInfoModel
        {
            Name = profileRes.Name,
            UUID = sPUuid
        };

        accountModel.Id = sPUuid.ToGuid();

        if (!string.IsNullOrEmpty(idToken))
        {
            var claims = JwtTokenHelper.GetTokenInfo(idToken);
            if (claims.TryGetValue("email", out var email))
                Email = email;
            else
                return new MicrosoftAuthResult
                {
                    AuthStatus = AuthStatus.Failed,
                    Error = new ErrorModel
                    {
                        Cause = "您需要在 scope 中添加：openid，email 和 profile 字段",
                        Error = "Azure应用配置错误",
                        ErrorMessage = "您需要在 scope 中添加：openid，email 和 profile 字段"
                    }
                };
        }

        return new MicrosoftAuthResult
        {
            Id = id ?? Guid.Empty,
            AccessToken = mcRes.AccessToken,
            AuthStatus = AuthStatus.Succeeded,
            Skin = profileRes.GetActiveSkin()?.Url,
            ExpiresIn = expiresIn,
            RefreshToken = refreshToken,
            CurrentAuthTime = DateTime.Now,
            SelectedProfile = sP,
            User = new UserInfoModel
            {
                UUID = sPUuid,
                UserName = profileRes.Name
            },
            Email = Email
        };
    }

    public AuthResultBase GetLastAuthResult()
    {
        var (_, value) = LauncherAccountParser.LauncherAccount.Accounts
            .FirstOrDefault(x =>
                x.Value.Username.Equals(Email, StringComparison.OrdinalIgnoreCase) &&
                x.Value.Type.Equals("XBox", StringComparison.OrdinalIgnoreCase));

        if (value == default)
            return default;

        var sP = new ProfileInfoModel
        {
            Name = value.MinecraftProfile.Name,
            UUID = new PlayerUUID(value.MinecraftProfile.Id)
        };

        return new MicrosoftAuthResult
        {
            AccessToken = value.AccessToken,
            AuthStatus = AuthStatus.Succeeded,
            Skin = value.Avatar,
            SelectedProfile = sP,
            User = new UserInfoModel
            {
                UUID = sP.UUID,
                UserName = sP.Name
            }
        };
    }

    public static void Configure(MicrosoftAuthenticatorAPISettings apiSettings)
    {
        ApiSettings = apiSettings;
    }

    public static object ResolveMSGraphResult<T>(string content)
    {
        var jsonObj = JsonDocument.Parse(content).RootElement;

        if (jsonObj.TryGetProperty("error", out _) && jsonObj.TryGetProperty("error_description", out _))
            return JsonConvert.DeserializeObject<GraphResponseErrorModel>(content);

        return JsonConvert.DeserializeObject<T>(content);
    }

    public async Task<GraphAuthResultModel?> GetMSAuthResult(Action<DeviceIdResponseModel> deviceTokenNotifier)
    {
        #region SEND DEVICE TOKEN REQUEST

        var deviceTokenRequestDic = new List<KeyValuePair<string, string>>
        {
            new("client_id", ApiSettings.ClientId),
            new("scope", string.Join(' ', ApiSettings.Scopes))
        };

        using var deviceTokenReq = new HttpRequestMessage(HttpMethod.Post, MSDeviceTokenRequestUrl)
        {
            Content = new FormUrlEncodedContent(deviceTokenRequestDic)
        };

        using var deviceTokenRes = await DefaultClient.SendAsync(deviceTokenReq);
        var deviceTokenContent = await deviceTokenRes.Content.ReadAsStringAsync();
        var deviceTokenModel = ResolveMSGraphResult<DeviceIdResponseModel>(deviceTokenContent);

        if (deviceTokenModel is not DeviceIdResponseModel deviceTokenResModel) return null;

        #endregion

        deviceTokenNotifier.Invoke(deviceTokenResModel);

        #region FETCH USER AUTH RESULT

        var userAuthResultDic = new List<KeyValuePair<string, string>>
        {
            new("grant_type", MSGrantType),
            new("client_id", ApiSettings.ClientId),
            new("device_code", deviceTokenResModel.DeviceCode)
        };

        GraphAuthResultModel result;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(deviceTokenResModel.Interval));

            using var userAuthResultReq = new HttpRequestMessage(HttpMethod.Post, MSDeviceTokenStatusUrl)
            {
                Content = new FormUrlEncodedContent(userAuthResultDic)
            };

            using var userAuthResultRes = await DefaultClient.SendAsync(userAuthResultReq);
            var userAuthResultContent = await userAuthResultRes.Content.ReadAsStringAsync();
            var userAuthResultModel = ResolveMSGraphResult<GraphAuthResultModel>(userAuthResultContent);

            if (userAuthResultModel is not GraphAuthResultModel)
                if (userAuthResultModel is GraphResponseErrorModel error)
                    switch (error.ErrorType)
                    {
                        case "authorization_pending":
                            continue;
                        case "authorization_declined":
                        case "expired_token":
                        case "bad_verification_code":
                            return null;
                    }

            result = (GraphAuthResultModel)userAuthResultModel;
            break;
        }

        #endregion

        return result;
    }

    public static string GetLoginUri(string clientId, string redirectUri)
    {
        return Uri.EscapeDataString("https://login.live.com/oauth20_authorize.srf"
                                    + $"?client_id={clientId}"
                                    + "&response_type=code"
                                    + $"&scope={MSAuthScope}"
                                    + $"&redirect_uri={redirectUri}");
    }

    async Task<T> SendRequest<T>(string url, object model)
    {
        var reqStr = JsonConvert.SerializeObject(model);

        using var res = await HttpHelper.Post(url, reqStr);

        if (!res.IsSuccessStatusCode) return default;

        var resStr = await res.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<T>(resStr);

        return result;
    }
}