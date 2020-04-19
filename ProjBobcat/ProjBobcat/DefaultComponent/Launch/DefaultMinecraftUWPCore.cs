using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    public class DefaultMinecraftUWPCore : IGameCore, IDisposable
    {
        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public string RootPath
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public Guid ClientToken
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [Obsolete("UWP 版本的Minecraft不需要该字段。")]
        public IVersionLocator VersionLocator
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [Obsolete("UWP 版本的Minecraft不需要该字段。")] public event EventHandler<GameExitEventArgs> GameExitEventDelegate;

        [Obsolete("UWP 版本的Minecraft不需要该字段。")] public event EventHandler<GameLogEventArgs> GameLogEventDelegate;

        [Obsolete("UWP 版本的Minecraft不需要该字段。")] public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate;

        public LaunchResult Launch(LaunchSettings launchSettings)
        {
            if (SystemInfoHelper.IsMinecraftUWPInstalled() == false) throw new InvalidOperationException();

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

        public void LogGameData(object sender, GameLogEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void LogLaunchData(object sender, LaunchLogEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void GameExit(object sender, GameExitEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void GameExit(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support

        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // NOTE: Leave out the finalizer altogether if this class doesn't
        // own unmanaged resources, but leave the other methods
        // exactly as they are.
        ~DefaultMinecraftUWPCore()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        #endregion
    }
}