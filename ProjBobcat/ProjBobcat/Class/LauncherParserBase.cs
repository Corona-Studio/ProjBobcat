namespace ProjBobcat.Class;

/// <summary>
///     提供了ProjBobcat启动器配置解析器的底层实现和预设属性
/// </summary>
public abstract class LauncherParserBase(string rootPath)
{
    protected string RootPath { get; init; } = rootPath;
}