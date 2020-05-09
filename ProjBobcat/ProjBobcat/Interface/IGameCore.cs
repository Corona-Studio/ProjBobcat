using System;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    /// <summary>
    ///     启动核心接口
    /// </summary>
    public interface IGameCore
    {
        string RootPath { get; set; }
        Guid ClientToken { get; set; }
        IVersionLocator VersionLocator { get; set; }
        LaunchResult Launch(LaunchSettings settings);
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

        /// <summary>
        ///     记录游戏日志调用方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LogGameData(object sender, GameLogEventArgs e);

        /// <summary>
        ///     记录启动器日志调用方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LogLaunchData(object sender, LaunchLogEventArgs e);

        /// <summary>
        ///     游戏退出调用方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void GameExit(object sender, GameExitEventArgs e);
    }
}