using System.Collections.Generic;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model
{
    /// <summary>
    ///     验证结果类
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        ///     验证状态
        /// </summary>
        public AuthStatus AuthStatus { get; set; }

        /// <summary>
        ///     获取的AccessToken
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        ///     选择的Profile
        /// </summary>
        public ProfileInfoModel SelectedProfile { get; set; }

        /// <summary>
        ///     可用的Profiles
        /// </summary>
        public List<ProfileInfoModel> Profiles { get; set; }

        /// <summary>
        ///     错误信息
        /// </summary>
        public ErrorModel Error { get; set; }

        /// <summary>
        ///     用户信息
        /// </summary>
        public UserInfoModel User { get; set; }

        /// <summary>
        /// 皮肤（微软账户）
        /// </summary>
        public string Skin { get; set; }
    }
}