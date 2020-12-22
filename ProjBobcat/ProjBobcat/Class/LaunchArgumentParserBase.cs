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
        private protected LaunchSettings LaunchSettings { get; set; }

        private protected ILauncherProfileParser LauncherProfileParser { get; set; }

        private protected IVersionLocator VersionLocator { get; set; }

        private protected AuthResultBase AuthResult { get; set; }

        private protected GameProfileModel GameProfile { get; set; }

        public virtual string NativeRoot =>
            GamePathHelper.GetNativeRoot(LaunchSettings.Version);

        public virtual string AssetRoot => GamePathHelper.GetAssetsRoot();

        private protected string ClassPath { get; set; }

        private protected VersionInfo VersionInfo { get; set; }

        private protected AuthResultBase LastAuthResult { get; set; }
    }
}