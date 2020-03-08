using System;
using System.Diagnostics;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Class
{
    public class LaunchWrapper
    {
        public LaunchWrapper(AuthResult authResult)
        {
            AuthResult = authResult;
        }

        public AuthResult AuthResult { get; }
        public int ExitCode { get; private set; }
        public IGameCore GameCore { get; set; }
        public Process Process { get; set; }

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