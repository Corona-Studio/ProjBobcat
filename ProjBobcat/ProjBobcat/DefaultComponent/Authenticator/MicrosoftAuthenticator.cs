using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.MicrosoftAuth;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.DefaultComponent.Authenticator
{
    public class MicrosoftAuthenticator : IAuthenticator
    {
        public const string MSClientId = "00000000402b5328";
        public const string MSAuthScope = "service::user.auth.xboxlive.com::MBI_SSL";

        public const string MSAuthRedirectUrl = "https://login.live.com/oauth20_desktop.srf";
        public const string MSAuthTokenUrl = " https://login.live.com/oauth20_token.srf";
        public const string MSAuthXBLUrl = "https://user.auth.xboxlive.com/user/authenticate";
        public const string MSAuthXSTSUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
        public const string MojangAuthUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
        public const string MojangOwnershipUrl = "https://api.minecraftservices.com/entitlements/mcstore";
        public const string MojangProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

        public static string MSLoginUrl => 
            Uri.EscapeUriString("https://login.live.com/oauth20_authorize.srf"
                                    + $"?client_id={MSClientId}"
                                    + "&response_type=code"
                                    + $"&scope={MSAuthScope}"
                                    + $"&redirect_uri={MSAuthRedirectUrl}");


        public string Email { get; set; }
        public string AuthCode { get; set; }

        public AuthType AuthType { get; set; }
        public string RefreshToken { get; set; }
        public long ExpiresIn { get; set; }
        public DateTime LastAuthTime { get; set; }

        public ILauncherAccountParser LauncherAccountParser { get; set; }

        public AuthResultBase Auth(bool userField = false)
        {
            return AuthTaskAsync(userField).GetAwaiter().GetResult();
        }

        public async Task<AuthResultBase> AuthTaskAsync(bool userField = false)
        {
            #region STAGE 1

            var reqForm = AuthType switch
            {
                AuthType.NormalAuth => Class.Model.MicrosoftAuth.AuthTokenRequestModel.Get(AuthCode),
                AuthType.RefreshToken => Class.Model.MicrosoftAuth.AuthTokenRequestModel.GetRefresh(RefreshToken),
                _ => Class.Model.MicrosoftAuth.AuthTokenRequestModel.Get(AuthCode)
            };

            if ((DateTime.Now - LastAuthTime).Hours >= 24 && AuthType == AuthType.NormalAuth)
            {
                if (string.IsNullOrEmpty(RefreshToken))
                {
                    return new AuthResultBase
                    {
                        Error = new ErrorModel
                        {
                            Cause = "由于 Token 已过期， 因此需要使用 RefreshToken 来刷新",
                            Error = "RefreshToken 为空",
                            ErrorMessage = "RefreshToken 为空"
                        }
                    };
                }


                reqForm = Class.Model.MicrosoftAuth.AuthTokenRequestModel.GetRefresh(RefreshToken);
            }

            if ((DateTime.Now - LastAuthTime).Hours < 24 && AuthType == AuthType.NormalAuth)
            {
                var result = GetLastAuthResult();
                if (result != default)
                    return result;
            }

            using var tokenResMessage =
                await HttpHelper.PostFormData(MSAuthTokenUrl, reqForm, "application/x-www-form-urlencoded");
            
            tokenResMessage.EnsureSuccessStatusCode();

            var tokenResStr = await tokenResMessage.Content.ReadAsStringAsync();
            var tokenRes = JsonConvert.DeserializeObject<AuthTokenResponseModel>(tokenResStr);

            #endregion

            #region STAGE 2

            var xBLRequestModel = AuthXBLRequestModel.Get(tokenRes.AccessToken);
            var xBLReqStr = JsonConvert.SerializeObject(xBLRequestModel);
            using var xBLResMessage = await HttpHelper.Post(MSAuthXBLUrl, xBLReqStr);
            var xBLResStr = await xBLResMessage.Content.ReadAsStringAsync();
            var xBLRes = JsonConvert.DeserializeObject<AuthXSResponseModel>(xBLResStr);

            #endregion

            #region STAGE 3

            var xSTSRequestModel = AuthXSTSRequestModel.Get(xBLRes.Token);
            var xSTSReqStr = JsonConvert.SerializeObject(xSTSRequestModel);
            using var xSTSResMessage = await HttpHelper.Post(MSAuthXSTSUrl, xSTSReqStr);
            var xSTSResStr = await xSTSResMessage.Content.ReadAsStringAsync();
            var xSTSRes = JsonConvert.DeserializeObject<AuthXSResponseModel>(xSTSResStr);

            #endregion

            #region STAGE 4

            var mcReqStr = JsonConvert.SerializeObject(new
            {
                identityToken = $"XBL3.0 x={xSTSRes.DisplayClaims["xui"].First()["uhs"]};{xSTSRes.Token}"
            });
            using var mcResMessage = await HttpHelper.Post(MojangAuthUrl, mcReqStr);
            var mcResStr = await mcResMessage.Content.ReadAsStringAsync();
            var mcRes = JsonConvert.DeserializeObject<AuthMojangResponseModel>(mcResStr);

            #endregion

            #region STAGE 5

            var ownResStr = await HttpHelper.Get(MojangOwnershipUrl, new Tuple<string, string>("Bearer", mcRes.AccessToken));
            var ownRes = JsonConvert.DeserializeObject<MojangOwnershipResponseModel>(ownResStr);

            if(!(ownRes.Items?.Any() ?? false))
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

            #endregion

            #region STAGE 6

            var profileResStr = await HttpHelper.Get(MojangProfileUrl, new Tuple<string, string>("Bearer", mcRes.AccessToken));
            var profileRes = JsonConvert.DeserializeObject<MojangProfileResponseModel>(profileResStr);

            #endregion

            var uuid = Guid.NewGuid().ToString("N");
            LauncherAccountParser.AddNewAccount(uuid, new AccountModel
            {
                AccessToken = mcRes.AccessToken,
                AccessTokenExpiresAt = DateTime.Now.AddSeconds(mcRes.ExpiresIn),
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
                RemoteId = mcRes.UserName,
                Type = "XBox",
                UserProperites = new List<AuthPropertyModel>(),
                Username = Email
            });

            var sPUuid = new PlayerUUID(profileRes.Id);
            var sP = new ProfileInfoModel
            {
                Name = profileRes.Name,
                UUID = sPUuid
            };

            return new MicrosoftAuthResult
            {
                AccessToken = mcRes.AccessToken,
                AuthStatus = AuthStatus.Succeeded,
                Skin = profileRes.GetActiveSkin()?.Url,
                ExpiresIn = tokenRes.ExpiresIn,
                RefreshToken = tokenRes.RefreshToken,
                CurrentAuthTime = DateTime.Now,
                SelectedProfile = sP,
                User = new UserInfoModel
                {
                    UUID = sPUuid,
                    UserName = profileRes.Name
                }
            };
        }

        public AuthResultBase GetLastAuthResult()
        {
            var (_, value) = LauncherAccountParser.LauncherAccount.Accounts
                .FirstOrDefault(x =>
                    x.Value.Username.Equals(Email, StringComparison.OrdinalIgnoreCase) &&
                    x.Value.Type.Equals("XBox", StringComparison.OrdinalIgnoreCase));

            if(value == default)
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
                ExpiresIn = ExpiresIn,
                RefreshToken = RefreshToken,
                CurrentAuthTime = DateTime.Now,
                SelectedProfile = sP,
                User = new UserInfoModel
                {
                    UUID = sP.UUID,
                    UserName = sP.Name
                }
            };
        }
    }
}