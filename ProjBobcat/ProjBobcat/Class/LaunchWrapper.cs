using System;
using System.Diagnostics;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    /// <summary>
    ///     启动包装类
    /// </summary>
    public class LaunchWrapper
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="authResult">验证结果</param>
        public LaunchWrapper(AuthResult authResult)
        {
            AuthResult = authResult;
        }

        /// <summary>
        ///     验证结果
        /// </summary>
        public AuthResult AuthResult { get; }

        /// <summary>
        ///     退出码
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        ///     游戏核心
        /// </summary>
        public IGameCore GameCore { get; set; }

        /// <summary>
        ///     游戏进程
        /// </summary>
        public Process Process { get; set; }

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
            if (e.Data == null)
                Process.ErrorDataReceived -= ProcessOnErrorDataReceived;
            else
                GameCore.LogGameData(sender, new GameLogEventArgs
                {
                    LogType = "error",
                    Content = e.Data
                });
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                Process.OutputDataReceived -= ProcessOnOutputDataReceived;
            else
                GameCore.LogGameData(sender, new GameLogEventArgs
                {
                    LogType = "log",
                    Content = e.Data
                });
        }
    }
}