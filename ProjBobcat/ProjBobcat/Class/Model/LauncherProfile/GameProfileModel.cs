using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    /// <summary>
    ///     Game Profile类
    /// </summary>
    public class GameProfileModel
    {
        /// <summary>
        ///     名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        ///     游戏目录
        /// </summary>
        [JsonProperty("gameDir")]
        public string GameDir { get; set; }

        /// <summary>
        ///     创建时间
        /// </summary>
        [JsonProperty("created")]
        public DateTime Created { get; set; }

        /// <summary>
        ///     Java虚拟机路径
        /// </summary>
        [JsonProperty("javaDir")]
        public string JavaDir { get; set; }

        /// <summary>
        ///     游戏窗口分辨率
        /// </summary>
        [JsonProperty("resolution")]
        public ResolutionModel Resolution { get; set; }

        /// <summary>
        ///     游戏图标
        /// </summary>
        [JsonProperty("icon")]
        public string Icon { get; set; }

        /// <summary>
        ///     Java虚拟机启动参数
        /// </summary>
        [JsonProperty("javaArgs")]
        public string JavaArgs { get; set; }

        /// <summary>
        ///     最后一次的版本Id
        /// </summary>
        [JsonProperty("lastVersionId")]
        public string LastVersionId { get; set; }

        /// <summary>
        ///     最后一次启动
        /// </summary>
        [JsonProperty("lastUsed")]
        public DateTime LastUsed { get; set; }

        /// <summary>
        ///     版本类型
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }
    }
}