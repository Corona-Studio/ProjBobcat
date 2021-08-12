using System.IO;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    /// <summary>
    ///     提供了ProjBobcat启动参数解析器的底层实现和预设属性
    /// </summary>
    public abstract class LaunchArgumentParserBase : LauncherParserBase
    {
        /// <summary>
        ///     启动设置
        /// </summary>
        private protected LaunchSettings LaunchSettings { get; set; }

        /// <summary>
        ///     launcher_profile 解析器
        /// </summary>
        private protected ILauncherProfileParser LauncherProfileParser { get; set; }

        /// <summary>
        ///     版本定位器
        /// </summary>

        private protected IVersionLocator VersionLocator { get; set; }

        /// <summary>
        ///     账户验证结果
        /// </summary>

        private protected AuthResultBase AuthResult { get; set; }

        /// <summary>
        ///     游戏档案
        /// </summary>

        private protected GameProfileModel GameProfile { get; set; }

        /// <summary>
        ///     Native 根目录
        /// </summary>
        public virtual string NativeRoot =>
            Path.Combine(RootPath, GamePathHelper.GetNativeRoot(LaunchSettings.Version));

        /// <summary>
        ///     Asset 根目录
        /// </summary>
        public virtual string AssetRoot => Path.Combine(RootPath, GamePathHelper.GetAssetsRoot());

        /// <summary>
        ///     Class 路径
        /// </summary>
        private protected string ClassPath { get; set; }

        /// <summary>
        ///     版本信息
        /// </summary>
        private protected VersionInfo VersionInfo { get; set; }

        /// <summary>
        ///     上一次的验证结果
        /// </summary>
        private protected AuthResultBase LastAuthResult { get; set; }
    }
}