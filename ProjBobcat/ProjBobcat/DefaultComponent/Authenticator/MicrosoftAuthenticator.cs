﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.Microsoft.Graph;
using ProjBobcat.Class.Model.MicrosoftAuth;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;
using ProjBobcat.JsonConverter;

namespace ProjBobcat.DefaultComponent.Authenticator;

#region Temp Models

public record CacheTokenProviderResult(
    bool IsCredentialValid,
    bool IsAuthResultValid,
    AuthResultBase? AuthResult,
    GraphAuthResultModel? CacheAuthResult);

record McReqModel(
    [property: JsonPropertyName("identityToken")]
    string IdentityToken);

[JsonSerializable(typeof(McReqModel))]
partial class McReqModelContext : JsonSerializerContext;

#endregion

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
        ArgumentNullException.ThrowIfNull(ApiSettings);
    }

    public static MicrosoftAuthenticatorAPISettings? ApiSettings { get; private set; }

    public static string MSDeviceTokenRequestUrl =>
        $"https://login.microsoftonline.com/{ApiSettings!.TenentId}/oauth2/v2.0/devicecode";

    public static string MSDeviceTokenStatusUrl =>
        $"https://login.microsoftonline.com/{ApiSettings!.TenentId}/oauth2/v2.0/token";

    public static string MSRefreshTokenRequestUrl =>
        $"https://login.microsoftonline.com/{ApiSettings!.TenentId}/oauth2/v2.0/token";

    public string? Email { get; set; }

    /// <summary>
    ///     MineCraft profile id, used to match the account history
    /// </summary>
    public Guid? ProfileId { get; set; }

    public Func<MicrosoftAuthenticator, ValueTask<CacheTokenProviderResult>>? CacheTokenProvider { get; init; }
    public required IHttpClientFactory HttpClientFactory { get; init; }
    public required ILauncherAccountParser LauncherAccountParser { get; init; }

    public AuthResultBase Auth(bool userField = false)
    {
        return this.AuthTaskAsync(userField).GetAwaiter().GetResult();
    }

    public async Task<AuthResultBase> AuthTaskAsync(bool userField = false)
    {
        if (this.CacheTokenProvider == null)
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

        var cacheTokenResult = await this.CacheTokenProvider(this);

        if (cacheTokenResult is
            {
                IsAuthResultValid: true,
                AuthResult: MicrosoftAuthResult { Error: null, AuthStatus: AuthStatus.Succeeded, ExpiresIn: >= 60 }
            })
            return cacheTokenResult.AuthResult;

        if (!cacheTokenResult.IsCredentialValid || cacheTokenResult.CacheAuthResult == null)
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

        var accessToken = cacheTokenResult.CacheAuthResult.AccessToken;
        var refreshToken = cacheTokenResult.CacheAuthResult.RefreshToken;
        var idToken = cacheTokenResult.CacheAuthResult.IdToken;

        #region STAGE 1

        var xBoxLiveToken =
            await this.SendRequest(MSAuthXBLUrl, AuthXBLRequestModel.Get(accessToken),
                AuthXSTSResponseModelContext.Default.AuthXSTSResponseModel,
                AuthXBLRequestModelContext.Default.AuthXBLRequestModel);

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

        var client = this.HttpClientFactory.CreateClient();

        using var xStsReq = new HttpRequestMessage(HttpMethod.Post, MSAuthXSTSUrl);
        xStsReq.Content = JsonContent.Create(
            AuthXSTSRequestModel.Get(xBoxLiveToken.Token),
            AuthXSTSRequestModelContext.Default.AuthXSTSRequestModel);

        using var xStsMessage = await client.SendAsync(xStsReq);

        if (!xStsMessage.IsSuccessStatusCode)
        {
            var errModel =
                await xStsMessage.Content.ReadFromJsonAsync(AuthXSTSErrorModelContext.Default.AuthXSTSErrorModel);
            var reason = (errModel?.XErr ?? 0) switch
            {
                2148916233 => "未创建 XBox 账户",
                2148916238 => "未成年人账户",
                _ => "未知"
            };

            var errorMessage = errModel?.Message ?? "未知";
            if (!string.IsNullOrEmpty(errModel?.Redirect))
                errorMessage += $"，相关链接：{errModel.Redirect}";

            var err = new ErrorModel
            {
                Cause = reason,
                Error = $"XSTS 认证失败，原因：{reason}",
                ErrorMessage = errorMessage
            };

            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = err
            };
        }

        var xStsRes =
            await xStsMessage.Content.ReadFromJsonAsync(AuthXSTSResponseModelContext.Default.AuthXSTSResponseModel);

        #endregion

        #region STAGE 2.5 (FETCH XBOX UID)

        using var xUidReq = new HttpRequestMessage(HttpMethod.Post, MSAuthXSTSUrl);
        xUidReq.Content = JsonContent.Create(
            AuthXSTSRequestModel.Get(xBoxLiveToken.Token, "http://xboxlive.com"),
            AuthXSTSRequestModelContext.Default.AuthXSTSRequestModel);

        using var xUidMessage = await client.SendAsync(xUidReq);

        var xuid = Guid.Empty.ToString("N");
        if (xUidMessage.IsSuccessStatusCode)
        {
            var xUidRes =
                await xUidMessage.Content.ReadFromJsonAsync(AuthXSTSResponseModelContext.Default.AuthXSTSResponseModel);

            if (xUidRes != null)
            {
                var isXUidXUiExists = xUidRes.DisplayClaims.TryGetProperty("xui", out var xuidXui);
                JsonElement? firstXuidXui = isXUidXUiExists ? xuidXui[0] : null;

                if (firstXuidXui.HasValue && firstXuidXui.Value.TryGetProperty("xid", out var xid) &&
                    xid.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(xid.GetString())) xuid = xid.GetString()!;
            }
        }

        #endregion

        #region STAGE 3

        var isXUiExists = xStsRes!.DisplayClaims.TryGetProperty("xui", out var xui);
        JsonElement? firstXui = isXUiExists ? xui[0] : null;
        if (!firstXui.HasValue || !firstXui.Value.TryGetProperty("uhs", out var uhs) ||
            uhs.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(uhs.GetString()))
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "无法从认证结果中获取 UHS 值",
                    Error = "XSTS 认证失败，原因：无法从认证结果中获取 UHS 值"
                }
            };

        var uhsValue = uhs.GetString()!;
        var mcReqModel = new McReqModel($"XBL3.0 x={uhsValue};{xStsRes.Token}");
        var mcRes = await this.SendRequest(MojangAuthUrl, mcReqModel,
            AuthMojangResponseModelContext.Default.AuthMojangResponseModel, McReqModelContext.Default.McReqModel);

        #endregion

        if (mcRes == null)
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Error = "Mojang 服务器返回了无效的响应",
                    ErrorMessage = "XSTS 认证失败，可能是网络原因导致的",
                    Cause = "XSTS 认证失败"
                }
            };

        #region STAGE 4

        using var ownershipReq = new HttpRequestMessage(HttpMethod.Get, MojangOwnershipUrl);
        ownershipReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcRes.AccessToken);

        using var ownershipRes = await client.SendAsync(ownershipReq);

        var ownership =
            await ownershipRes.Content.ReadFromJsonAsync(MojangOwnershipResponseModelContext.Default
                .MojangOwnershipResponseModel);

        if (ownership?.Items == null || ownership.Items.Length == 0)
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

        using var profileReq = new HttpRequestMessage(HttpMethod.Get, MojangProfileUrl);
        profileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcRes.AccessToken);

        using var profileRes = await client.SendAsync(profileReq);

        MojangProfileResponseModel? profile;

        try
        {
            profile =
                await profileRes.Content.ReadFromJsonAsync(MojangProfileResponseModelContext.Default
                    .MojangProfileResponseModel);
        }
        catch (JsonException e)
        {
            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "NOPROFILE",
                    Error = "无法从服务器拉取用户档案，用户还没有建立 Mojang Profile",
                    ErrorMessage = e.ToString()
                }
            };
        }

        if (!profileRes.IsSuccessStatusCode || profile == null)
        {
            var errModel =
                await profileRes.Content.ReadFromJsonAsync(MojangErrorResponseModelContext.Default
                    .MojangErrorResponseModel);

            return new MicrosoftAuthResult
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "NOPROFILE",
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
            AccessTokenExpiresAt = DateTime.Now.AddSeconds(mcRes.ExpiresIn),
            Avatar = profile.GetActiveSkin()?.Url,
            Cape = profile.GetActiveCape()?.Url,
            EligibleForMigration = false,
            HasMultipleProfiles = false,
            Legacy = false,
            LocalId = uuid,
            MinecraftProfile = new AccountProfileModel
            {
                Id = profile.Id,
                Name = profile.Name
            },
            Persistent = true,
            RemoteId = profile.Name,
            Type = "XBox",
            UserProperites = null,
            Username = profile.Name
        };

        if (!this.LauncherAccountParser.AddOrReplaceAccount(uuid, accountModel, out var id))
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

        var sPUuid = new Guid(profile.Id);
        var sP = new ProfileInfoModel
        {
            Name = profile.Name,
            Id = sPUuid
        };

        accountModel.Id = sPUuid;

        if (!string.IsNullOrEmpty(idToken))
        {
            var claims = JwtTokenHelper.GetTokenInfo(idToken);
            if (claims.TryGetValue("email", out var email))
                this.Email = email;
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
            Skin = profile.GetActiveSkin()?.Url,
            Cape = profile.GetActiveCape()?.Url,
            ExpiresIn = mcRes.ExpiresIn,
            RefreshToken = refreshToken,
            CurrentAuthTime = DateTime.Now,
            SelectedProfile = sP,
            User = new UserInfoModel
            {
                Id = sPUuid,
                UserName = profile.Name
            },
            Email = this.Email,
            XBoxUid = xuid
        };
    }

    public AuthResultBase? GetLastAuthResult()
    {
        var (_, value) = this.LauncherAccountParser.LauncherAccount.Accounts!
            .FirstOrDefault(x =>
                (x.Value.MinecraftProfile?.Id.Equals(this.ProfileId?.ToString("N"),
                    StringComparison.OrdinalIgnoreCase) ?? false) &&
                x.Value.Type.Equals("XBox", StringComparison.OrdinalIgnoreCase));

        if (value == null)
            return null;

        var sP = new ProfileInfoModel
        {
            Name = value.MinecraftProfile?.Name ?? this.Email ?? string.Empty,
            Id = new Guid(value.MinecraftProfile?.Id ?? this.Email ?? string.Empty)
        };

        return new MicrosoftAuthResult
        {
            AccessToken = value.AccessToken,
            ExpiresIn = (long)(value.AccessTokenExpiresAt - DateTime.Now).TotalSeconds,
            AuthStatus = AuthStatus.Succeeded,
            Skin = value.Avatar,
            Cape = value.Cape,
            SelectedProfile = sP,
            User = new UserInfoModel
            {
                Id = sP.Id,
                UserName = sP.Name
            }
        };
    }

    public static void Configure(MicrosoftAuthenticatorAPISettings apiSettings)
    {
        ApiSettings = apiSettings;
    }

    public static object? ResolveMSGraphResult<T>(string content, JsonTypeInfo<T> typeInfo)
    {
        var jsonObj = JsonDocument.Parse(content).RootElement;
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new DateTimeConverterUsingDateTimeParse()
            }
        };

        if (jsonObj.TryGetProperty("error", out _) && jsonObj.TryGetProperty("error_description", out _))
            return JsonSerializer.Deserialize(content, typeof(GraphResponseErrorModel),
                new GraphResponseErrorModelContext(options)) as GraphResponseErrorModel;

        return JsonSerializer.Deserialize(content, typeInfo);
    }

    public static async Task<GraphAuthResultModel?> GetMSAuthResult(
        IHttpClientFactory httpClientFactory,
        Action<DeviceIdResponseModel> deviceTokenNotifier)
    {
        #region SEND DEVICE TOKEN REQUEST

        var client = httpClientFactory.CreateClient();
        var deviceTokenRequestDic = new[]
        {
            new KeyValuePair<string, string>("client_id", ApiSettings!.ClientId),
            new KeyValuePair<string, string>("scope", string.Join(' ', ApiSettings.Scopes))
        };

        using var deviceTokenReq = new HttpRequestMessage(HttpMethod.Post, MSDeviceTokenRequestUrl);

        deviceTokenReq.Content = new FormUrlEncodedContent(deviceTokenRequestDic);

        using var deviceTokenRes = await client.SendAsync(deviceTokenReq);

        var deviceTokenContent = await deviceTokenRes.Content.ReadAsStringAsync();
        var deviceTokenModel =
            ResolveMSGraphResult(deviceTokenContent, DeviceIdResponseModelContext.Default.DeviceIdResponseModel);

        if (deviceTokenModel is not DeviceIdResponseModel deviceTokenResModel) return null;

        #endregion

        deviceTokenNotifier.Invoke(deviceTokenResModel);

        #region FETCH USER AUTH RESULT

        var userAuthResultDic = new[]
        {
            new KeyValuePair<string, string>("grant_type", MSGrantType),
            new KeyValuePair<string, string>("client_id", ApiSettings.ClientId),
            new KeyValuePair<string, string>("device_code", deviceTokenResModel.DeviceCode)
        };

        GraphAuthResultModel? result;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(deviceTokenResModel.Interval + 2));

            using var userAuthResultReq = new HttpRequestMessage(HttpMethod.Post, MSDeviceTokenStatusUrl);

            userAuthResultReq.Content = new FormUrlEncodedContent(userAuthResultDic);

            using var userAuthResultRes = await client.SendAsync(userAuthResultReq);

            var userAuthResultContent = await userAuthResultRes.Content.ReadAsStringAsync();
            var userAuthResultModel = ResolveMSGraphResult(userAuthResultContent,
                GraphAuthResultModelContext.Default.GraphAuthResultModel);

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

            result = (GraphAuthResultModel?)userAuthResultModel;
            break;
        }

        #endregion

        return result;
    }

    private async Task<T?> SendRequest<T, TReq>(
        string url,
        TReq model,
        JsonTypeInfo<T> typeInfo,
        JsonTypeInfo<TReq> reqTypeInfo)
    {
        var client = this.HttpClientFactory.CreateClient();
        var content = JsonContent.Create(model, reqTypeInfo);

        using var res = await client.PostAsync(url, content);

        if (!res.IsSuccessStatusCode) return default;

        var result = await res.Content.ReadFromJsonAsync(typeInfo);

        return result;
    }
}