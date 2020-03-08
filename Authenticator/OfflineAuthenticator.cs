using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.Authenticator
{
    public class OfflineAuthenticator : IAuthenticator
    {
        public string Username { get; set; }
        public ILauncherProfileParser LauncherProfileParser { get; set; }

        public AuthResult Auth(bool userField)
        {
            var authProperty = new AuthPropertyModel
            {
                Name = "preferredLanguage",
                ProfileId = "",
                UserId = Guid.NewGuid().ToString("N"),
                Value = "zh-cn"
            };

            var calcGuid = GuidHelper.GetGuidByName(Username).ToString("N");
            var result = new AuthResult
            {
                AccessToken = Guid.NewGuid().ToString("N"),
                AuthStatus = AuthStatus.Succeeded,
                SelectedProfile = new ProfileInfoModel
                {
                    Name = Username,
                    Id = calcGuid
                },
                User = new UserInfoModel
                {
                    Id = calcGuid,
                    Properties = new List<PropertyModel>
                    {
                        new PropertyModel
                        {
                            Name = authProperty.Name,
                            Value = authProperty.Value
                        }
                    }
                }
            };

            var authInfo = new AuthInfoModel
            {
                UserName = Username,
                Profiles = new Dictionary<string, AuthProfileModel>
                {
                    {result.SelectedProfile.Id, new AuthProfileModel {DisplayName = Username}}
                },
                Properties = new List<AuthPropertyModel>
                {
                    authProperty
                }
            };
            LauncherProfileParser.AddNewAuthInfo(authInfo, calcGuid);

            return result;
        }

        /// <summary>
        ///     异步验证凭据（不可用）
        /// </summary>
        /// <param name="userField"></param>
        /// <returns></returns>
        public Task<AuthResult> AuthTaskAsync(bool userField)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     验证凭据（同步）
        /// </summary>
        /// <param name="userField"></param>
        /// <returns></returns>
        public AuthResult GetLastAuthResult()
        {
            return Auth(false);
        }
    }
}