using ProjBobcat.Interface;

namespace ProjBobcat.Class;

/// <summary>
///     提供了ProjBobcat启动参数解析器的底层实现和预设属性
/// </summary>
public abstract class LaunchArgumentParserBase(
    string rootPath,
    ILauncherProfileParser launcherProfileParser,
    IVersionLocator versionLocator)
    : LauncherParserBase(rootPath)
{
    /// <summary>
    ///     launcher_profile 解析器
    /// </summary>
    protected ILauncherProfileParser LauncherProfileParser { get; init; } = launcherProfileParser;

    /// <summary>
    ///     版本定位器
    /// </summary>
    protected IVersionLocator VersionLocator { get; init; } = versionLocator;
}