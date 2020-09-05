using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    /// <summary>
    ///     提供了UWP版本MineCraft的启动核心
    /// </summary>
    public class DefaultMineCraftUWPCore : IGameCore
    {
        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public string RootPath
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public Guid ClientToken
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public IVersionLocator VersionLocator
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public event EventHandler<GameExitEventArgs> GameExitEventDelegate;

        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public event EventHandler<GameLogEventArgs> GameLogEventDelegate;

        /// <summary>
        ///     无用字段
        /// </summary>
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate;

        public LaunchResult Launch(LaunchSettings launchSettings)
        {
            if (!SystemInfoHelper.IsMinecraftUWPInstalled()) throw new InvalidOperationException();

            using var process = new Process
                {StartInfo = new ProcessStartInfo {UseShellExecute = true, FileName = "minecraft:"}};
            process.Start();

            return default;
        }

        [Obsolete("UWP启动核心并不支持异步启动")]
        public Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        public void LogGameData(object sender, GameLogEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        public void LogLaunchData(object sender, LaunchLogEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     无用字段
        /// </summary>
        public void GameExit(object sender, GameExitEventArgs e)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support

        /// <summary>
        ///     IDisposable保留字段
        /// </summary>
        public void Dispose()
        {
        }

        #endregion

        private static void GameExit(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}