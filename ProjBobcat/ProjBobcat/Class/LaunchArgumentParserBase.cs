using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    public abstract class LaunchArgumentParserBase
    {
        private protected LaunchSettings LaunchSettings { get; set; }

        private protected ILauncherProfileParser LauncherProfileParser { get; set; }

        private protected IVersionLocator VersionLocator { get; set; }

        private protected AuthResult AuthResult { get; set; }

        private protected GameProfileModel GameProfile { get; set; }

        public virtual string NativeRoot =>
            GamePathHelper.GetNativeRoot(RootPath, LaunchSettings.Version);

        public virtual string AssetRoot => GamePathHelper.GetAssetsRoot(RootPath);

        private protected string ClassPath { get; set; }

        private protected string RootPath { get; set; }

        private protected VersionInfo VersionInfo { get; set; }

        private protected AuthResult LastAuthResult { get; set; }
    }
}