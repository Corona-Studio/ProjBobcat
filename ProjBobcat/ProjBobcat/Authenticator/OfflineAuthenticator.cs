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
    /// <summary>
    /// 表示一个离线凭据验证器。
    /// </summary>
    public class OfflineAuthenticator : IAuthenticator
    {
        /// <summary>
        /// 获取或设置用户名。
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// 获取或设置启动程序配置文件分析器。
        /// </summary>
        public ILauncherProfileParser LauncherProfileParser { get; set; }
        /// <summary>
        /// 验证凭据。
        /// </summary>
        /// <param name="userField">该参数将被忽略。</param>
        /// <returns>身份验证结果。</returns>
        public AuthResult Auth(bool userField = false)
        {
            var authProperty = new AuthPropertyModel
            {
                Name = "preferredLanguage",
                ProfileId = "",
                UserId = PlayerUUID.Random(),
                Value = "zh-cn"
            };

            var uuid = PlayerUUID.FromOfflinePlayerName(Username);
            var result = new AuthResult
            {
                AccessToken = GuidHelper.NewGuidString(),
                AuthStatus = AuthStatus.Succeeded,
                SelectedProfile = new ProfileInfoModel
                {
                    Name = Username,
                    UUID = uuid
                },
                User = new UserInfoModel
                {
                    UUID = uuid,
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
                Profiles = new Dictionary<PlayerUUID, AuthProfileModel>
                {
                    {result.SelectedProfile.UUID, new AuthProfileModel {DisplayName = Username}}
                },
                Properties = new List<AuthPropertyModel>
                {
                    authProperty
                }
            };
            LauncherProfileParser.AddNewAuthInfo(authInfo, uuid);

            return result;
        }

        /// <summary>
        /// 异步验证凭据。
        /// </summary>
        /// <param name="userField">改参数将被忽略。</param>
        /// <returns></returns>
        [Obsolete("此方法已过时，请使用其同步版本 Auth(bool) 。", true)]
        public Task<AuthResult> AuthTaskAsync(bool userField)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 验证凭据。
        /// </summary>
        /// <returns>验证结果。</returns>
        [Obsolete("此方法已过时，请使用 Auth(bool) 代替。")]
        public AuthResult GetLastAuthResult()
        {
            return Auth(false);
        }
    }
}