using System;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

/// <summary>
///     启动核心接口
/// </summary>
public interface IGameCore : IDisposable
{
    /// <summary>
    ///     获取或设置根目录。
    /// </summary>
    string RootPath { get; init; }

    /// <summary>
    ///     获取或设置客户端令牌。
    /// </summary>
    Guid ClientToken { get; init; }

    /// <summary>
    ///     获取或设置版本定位器。
    /// </summary>
    VersionLocatorBase VersionLocator { get; init; }

    IGameLogResolver? GameLogResolver { get; init; }

    /// <summary>
    ///     启动游戏。
    ///     若启动成功，其返回值会包含消耗的时间；失败则包含异常信息。
    /// </summary>
    /// <param name="settings">启动设置。</param>
    /// <returns>启动结果。若启动成功，会包含消耗的时间；失败则包含异常信息。</returns>
    LaunchResult Launch(LaunchSettings settings);

    /// <summary>
    ///     启动游戏。
    ///     若启动成功，其返回值会包含消耗的时间；失败则包含异常信息。
    /// </summary>
    /// <param name="settings">启动设置。</param>
    /// <returns>启动结果。若启动成功，会包含消耗的时间；失败则包含异常信息。</returns>
    Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings);

    /// <summary>
    ///     游戏退出事件
    /// </summary>
    event EventHandler<GameExitEventArgs> GameExitEventDelegate;

    /// <summary>
    ///     游戏日志输出事件
    /// </summary>
    event EventHandler<GameLogEventArgs> GameLogEventDelegate;

    /// <summary>
    ///     启动日志输出事件
    /// </summary>
    event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate;
}