using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ForgeInstaller
{
    public class HighVersionForgeInstaller : IForgeInstaller
    {
        private const string InstallArgumentPlaceHolder =
            "-cp {ClassPaths} me.xfl03.HeadlessInstaller -progress -installClient {InstallPath}";

        private const string HeadlessInstallerDownloadUri =
            "https://csl.littleservice.cn/libraries/me/xfl03/forge-installer-headless/1.0.1/forge-installer-headless-1.0.1.jar";

        private double _currentProgress;

        private string _currentStage = "未知";

        private bool _succeed;
        public string JavaExecutablePath { get; set; }

        public string ForgeExecutablePath { get; set; }
        public string ForgeInstallPath { get; set; }

        public event EventHandler<ForgeInstallStageChangedEventArgs> StageChangedEventDelegate;

        public ForgeInstallResult InstallForge()
        {
            throw new NotImplementedException();
        }

        public async Task<ForgeInstallResult> InstallForgeTaskAsync()
        {
            if (string.IsNullOrEmpty(ForgeExecutablePath))
                throw new ArgumentNullException("未指定\"ForgeExecutablePath\"参数");
            if (string.IsNullOrEmpty(ForgeInstallPath))
                throw new ArgumentNullException("未指定\"ForgeInstallPath\"参数");
            if (string.IsNullOrEmpty(JavaExecutablePath))
                throw new ArgumentNullException("未指定\"JavaExecutablePath\"参数");

            if (!File.Exists(JavaExecutablePath))
                return new ForgeInstallResult
                {
                    Succeeded = false,
                    Error = new ErrorModel
                    {
                        Cause = "找不到Java可执行文件",
                        Error = "Headless安装工具安装前准备失败",
                        ErrorMessage = "找不到Java可执行文件，请确认您的路径是否正确"
                    }
                };

            if (!File.Exists(ForgeExecutablePath))
                return new ForgeInstallResult
                {
                    Succeeded = false,
                    Error = new ErrorModel
                    {
                        Cause = "找不到Forge可执行文件",
                        Error = "安装前准备失败",
                        ErrorMessage = "找不到Forge可执行文件，请确认您的路径是否正确"
                    }
                };

            var di = new DirectoryInfo(ForgeInstallPath);
            if (!di.Exists)
                di.Create();

            di.CreateSubdirectory("Temp");

            var taskResult = await DownloadHelper.DownloadSingleFileAsync(new Uri(HeadlessInstallerDownloadUri),
                $"{di.FullName}Temp", "HeadlessInstaller.jar").ConfigureAwait(false);

            if (taskResult.TaskStatus == TaskResultStatus.Error)
                return new ForgeInstallResult
                {
                    Succeeded = false,
                    Error = new ErrorModel
                    {
                        Cause = "Headless安装工具下载",
                        Error = "Headless安装工具下载失败",
                        ErrorMessage = "Headless安装工具下载失败，请检查您的网络连接"
                    }
                };

            var classes = new List<string>
            {
                $"{di.FullName}Temp\\HeadlessInstaller.jar",
                ForgeExecutablePath
            };

            var replacementDic = new Dictionary<string, string>
            {
                {"{ClassPaths}", $"\"{string.Join(";", classes)}\""},
                {"{InstallPath}", $"\"{ForgeInstallPath}\""}
            };

            var installArgument = StringHelper.ReplaceByDic(InstallArgumentPlaceHolder, replacementDic);

            var process = Process.Start(new ProcessStartInfo(JavaExecutablePath, installArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = ForgeInstallPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            process.OutputDataReceived += ProcessOutput;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            await Task.Run(process.WaitForExit).ConfigureAwait(false);

            try
            {
                DirectoryHelper.CleanDirectory($"{di.FullName}Temp\\", true);

                return new ForgeInstallResult
                {
                    Error = null,
                    Succeeded = _succeed
                };
            }
            catch (Exception ex)
            {
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Error = "安装失败",
                        Exception = ex
                    },
                    Succeeded = _succeed
                };
            }
        }

        private void ProcessOutput(object sender, DataReceivedEventArgs args)
        {
            if (double.TryParse(args.Data, out var progress))
            {
                _currentProgress = progress / 100;
                InvokeStatusChangedEvent(_currentStage, _currentProgress);
                return;
            }

            if (args.Data.StartsWith("START", StringComparison.Ordinal))
            {
                _currentStage = args.Data.Substring(7);
                InvokeStatusChangedEvent(_currentStage, _currentProgress);
                return;
            }

            if (!args.Data.StartsWith("STAGE", StringComparison.Ordinal)) return;

            _currentStage = args.Data.Substring(7);

            if (_currentStage.Equals("INSTALL SUCCESSFUL", StringComparison.Ordinal))
            {
                _succeed = true;
                return;
            }

            if (_currentStage.Equals("INSTALL FAILED", StringComparison.Ordinal))
            {
                _succeed = false;
                return;
            }

            InvokeStatusChangedEvent(_currentStage, _currentProgress);
        }

        private void InvokeStatusChangedEvent(string currentStage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new ForgeInstallStageChangedEventArgs
            {
                CurrentStage = currentStage,
                Progress = progress
            });
        }
    }
}