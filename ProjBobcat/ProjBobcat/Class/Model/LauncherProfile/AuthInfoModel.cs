using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    /// <summary>
    /// 验证信息模型
    /// </summary>
    public class AuthInfoModel
    {
        /// <summary>
        /// AccessToken
        /// </summary>
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        /// <summary>
        /// Auth Profile集合
        /// </summary>
        [JsonProperty("profiles")]
        public Dictionary<PlayerUUID, AuthProfileModel> Profiles { get; set; }

        /// <summary>
        /// User Properties集合
        /// </summary>
        [JsonProperty("properties")]
        public List<AuthPropertyModel> Properties { get; set; }

        /// <summary>
        /// 游戏名称
        /// </summary>
        [JsonProperty("username")]
        public string UserName { get; set; }
    }
}