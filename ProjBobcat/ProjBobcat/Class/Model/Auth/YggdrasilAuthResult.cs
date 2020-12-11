using System.Collections.Generic;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model.Auth
{
    /// <summary>
    ///     验证结果类
    /// </summary>
    public class YggdrasilAuthResult : AuthResultBase
    {
        /// <summary>
        ///     可用的Profiles
        /// </summary>
        public List<ProfileInfoModel> Profiles { get; set; }
    }
}