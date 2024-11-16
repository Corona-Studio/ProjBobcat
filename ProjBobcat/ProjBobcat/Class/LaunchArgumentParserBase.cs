using System.IO;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

/// <summary>
///     提供了ProjBobcat启动参数解析器的底层实现和预设属性
/// </summary>
public abstract class LaunchArgumentParserBase(
    string rootPath,
    LaunchSettings launchSettings,
    ILauncherProfileParser launcherProfileParser,
    IVersionLocator versionLocator,
    AuthResultBase authResult)
    : LauncherParserBase(rootPath)
{
    /// <summary>
    ///     启动设置
    /// </summary>
    protected LaunchSettings LaunchSettings { get; init; } = launchSettings;

    /// <summary>
    ///     launcher_profile 解析器
    /// </summary>
    protected ILauncherProfileParser LauncherProfileParser { get; init; } = launcherProfileParser;

    /// <summary>
    ///     版本定位器
    /// </summary>
    protected IVersionLocator VersionLocator { get; init; } = versionLocator;

    /// <summary>
    ///     账户验证结果
    /// </summary>
    protected AuthResultBase AuthResult { get; init; } = authResult;

    /// <summary>
    ///     游戏档案
    /// </summary>
    protected GameProfileModel? GameProfile { get; init; }

    /// <summary>
    ///     Native 根目录
    /// </summary>
    public virtual string NativeRoot =>
        Path.Combine(this.RootPath, GamePathHelper.GetNativeRoot(this.LaunchSettings.Version));

    /// <summary>
    ///     Asset 根目录
    /// </summary>
    public virtual string AssetRoot => Path.Combine(this.RootPath, GamePathHelper.GetAssetsRoot());

    /// <summary>
    ///     Class 路径
    /// </summary>
    protected abstract string ClassPath { get; init; }

    /// <summary>
    ///     版本信息
    /// </summary>
    protected abstract VersionInfo VersionInfo { get; init; }

    /// <summary>
    ///     上一次的验证结果
    /// </summary>
    protected AuthResultBase? LastAuthResult { get; init; }
}