using System;
using System.Diagnostics;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    /// <summary>
    ///     启动包装类
    /// </summary>
    public class LaunchWrapper : IDisposable
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="authResult">验证结果</param>
        public LaunchWrapper(AuthResultBase authResult)
        {
            AuthResult = authResult;
        }

        /// <summary>
        ///     验证结果
        /// </summary>
        public AuthResultBase AuthResult { get; }

        /// <summary>
        ///     退出码
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        ///     游戏核心
        /// </summary>
        public IGameCore GameCore { get; init; }

        /// <summary>
        ///     游戏进程
        /// </summary>
        public Process Process { get; init; }

        public void Dispose()
        {
        }

        /// <summary>
        ///     执行过程
        /// </summary>
        public void Do()
        {
            Process.BeginOutputReadLine();
            Process.OutputDataReceived += ProcessOnOutputDataReceived;
            Process.BeginErrorReadLine();
            Process.ErrorDataReceived += ProcessOnErrorDataReceived;
            Process.Exited += ProcessOnExited;
        }

        private void ProcessOnExited(object sender, EventArgs e)
        {
            ExitCode = Process.ExitCode;
        }

        private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            GameCore.LogGameData(sender, new GameLogEventArgs
            {
                LogType = GameLogType.Unknown,
                RawContent = e.Data
            });
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            var totalPrefix = GameCore.GameLogResolver.ResolveTotalPrefix(e.Data);
            var type = GameCore.GameLogResolver.ResolveLogType(string.IsNullOrEmpty(totalPrefix)
                ? e.Data
                : totalPrefix);

            if (type is GameLogType.ExceptionMessage or GameLogType.StackTrace)
            {
                var exceptionMsg = GameCore.GameLogResolver.ResolveExceptionMsg(e.Data);
                var stackTrace = GameCore.GameLogResolver.ResolveStackTrace(e.Data);

                GameCore.LogGameData(sender, new GameLogEventArgs
                {
                    LogType = type,
                    RawContent = e.Data,
                    StackTrace = stackTrace,
                    ExceptionMsg = exceptionMsg
                });

                return;
            }

            var time = GameCore.GameLogResolver.ResolveTime(totalPrefix);
            var source = GameCore.GameLogResolver.ResolveSource(totalPrefix);
            

            GameCore.LogGameData(sender, new GameLogEventArgs
            {
                LogType = type,
                RawContent = e.Data,
                Content = e.Data[(totalPrefix?.Length ?? 0)..],
                Source = source,
                Time = time
            });
        }
    }
}