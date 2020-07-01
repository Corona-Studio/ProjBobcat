using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    /// <summary>
    /// Auth Profile类
    /// </summary>
    public class AuthProfileModel
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        [JsonProperty("displayName")] public string DisplayName { get; set; }
    }
}