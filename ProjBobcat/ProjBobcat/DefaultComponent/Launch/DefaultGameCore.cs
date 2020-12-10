using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ProjBobcat.DefaultComponent.Launch
{
    /// <summary>
    ///     表示一个默认的游戏核心。
    /// </summary>
    public class DefaultGameCore : IGameCore
    {
        private string _rootPath;

        public IArgumentParser LaunchArgumentParser { get; set; }

        public string RootPath
        {
            get => _rootPath;
            set
            {
                if (string.IsNullOrEmpty(value))
                    return;

                _rootPath = Path.GetFullPath(value.TrimEnd('/'));
            }
        }

        public IVersionLocator VersionLocator { get; set; }

        public Guid ClientToken { get; set; }

        public event EventHandler<GameExitEventArgs> GameExitEventDelegate;
        public event EventHandler<GameLogEventArgs> GameLogEventDelegate;
        public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate;

        public LaunchResult Launch(LaunchSettings settings)
        {
            return LaunchTaskAsync(settings).GetAwaiter().GetResult();
        }

        public async Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
        {
            try
            {
                //逐步测量启动时间。
                //Measure the launch time step by step.
                var prevSpan = new TimeSpan();
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                #region 解析游戏 Game Info Resolver

                var version = VersionLocator.GetGame(settings.Version);

                //在以下方法中，我们存储前一个步骤的时间并且重置秒表，以此逐步测量启动时间。
                //In the method InvokeLaunchLogThenStart(args), we storage the time span of the previous process and restart the watch in order that the time used in each step is recorded.
                InvokeLaunchLogThenStart("解析游戏", ref prevSpan, ref stopwatch);

                //错误处理
                //Error processor
                if (version == null)
                    return new LaunchResult
                    {
                        ErrorType = LaunchErrorType.OperationFailed,
                        Error = new ErrorModel
                        {
                            Error = "解析游戏失败",
                            ErrorMessage = "我们在解析游戏时出现了错误",
                            Cause = "这有可能是因为您的游戏JSON文件损坏所导致的问题"
                        }
                    };

                #endregion

                #region 验证账户凭据 Legal Account Verifier

                //以下代码实现了账户模式从离线到在线的切换。
                //The following code switches account mode between offline and yggdrasil.
                var authResult = settings.Authenticator switch
                {
                    OfflineAuthenticator off => off.Auth(),
                    YggdrasilAuthenticator ygg => await ygg.AuthTaskAsync(true),
                    MicrosoftAuthenticator mic => await mic.AuthTaskAsync(),
                    _ => null
                };
                InvokeLaunchLogThenStart("验证账户凭据", ref prevSpan, ref stopwatch);

                //错误处理
                //Error processor
                if (authResult == null || authResult.AuthStatus == AuthStatus.Failed ||
                    authResult.AuthStatus == AuthStatus.Unknown)
                    return new LaunchResult
                    {
                        LaunchSettings = settings,
                        Error = new ErrorModel
                        {
                            Error = "验证失败",
                            Cause = authResult == null
                                ? "未知的验证器"
                                : authResult.AuthStatus switch
                                {
                                    AuthStatus.Failed => "可能是因为用户名或密码错误，或是验证服务器暂时未响应",
                                    AuthStatus.Unknown => "未知错误",
                                    _ => "未知错误"
                                },
                            ErrorMessage = "无法验证凭据的有效性"
                        }
                    };

                if (authResult.SelectedProfile == default && settings.SelectedProfile == default)
                    return new LaunchResult
                    {
                        LaunchSettings = settings,
                        Error = new ErrorModel
                        {
                            Error = "验证失败",
                            Cause = "没有选择用于启动游戏的Profile",
                            ErrorMessage = "没有选择任何Profile"
                        }
                    };

                if (settings.SelectedProfile != default)
                    authResult.SelectedProfile = settings.SelectedProfile;

                #endregion

                #region 解析启动参数 Launch Parameters Resolver

                var javasArr = new[]
                {
                    settings.GameArguments?.JavaExecutable,
                    settings.FallBackGameArguments?.JavaExecutable
                };
                var isJavaExists = false;

                foreach (var java in javasArr)
                    if (!string.IsNullOrEmpty(java) && File.Exists(java))
                        isJavaExists = true;

                if (!isJavaExists)
                    return new LaunchResult
                    {
                        ErrorType = LaunchErrorType.NoJava,
                        Error = new ErrorModel
                        {
                            Cause = "未找到JRE运行时，可能是输入的路劲为空或出错，亦或是指定的文件并不存在。",
                            Error = "未找到JRE运行时",
                            ErrorMessage = "输入的路劲为空或出错，亦或是指定的文件并不存在"
                        }
                    };

                var argumentParser = new DefaultLaunchArgumentParser(settings, VersionLocator.LauncherProfileParser,
                    VersionLocator, authResult, RootPath, version.RootVersion);

                //以字符串数组形式生成启动参数。
                //Generates launch cmd arguments in string[].
                var arguments = argumentParser.GenerateLaunchArguments();
                InvokeLaunchLogThenStart("解析启动参数", ref prevSpan, ref stopwatch);

                var sb = new StringBuilder();

                if (arguments.Count != 6)
                    return new LaunchResult
                    {
                        ErrorType = LaunchErrorType.IncompleteArguments,
                        Error = new ErrorModel
                        {
                            Cause = "启动核心生成的参数不完整",
                            Error = "重要参数缺失",
                            ErrorMessage = "启动参数不完整，很有可能是缺少Java路径导致的"
                        }
                    };

                //从参数数组中移出java路径并加以存储。
                //Load the first element(java's path) into the excutable string and removes it from the generated arguments
                var executable = arguments[0];
                arguments.RemoveAt(0);

                //通过String Builder格式化参数。（转化成字符串）
                //Format the arguments using string builder.(Convert to string)
                arguments.ForEach(arg => sb.Append(arg.Trim()).Append(' '));
                InvokeLaunchLogThenStart(sb.ToString(), ref prevSpan, ref stopwatch);

                #endregion

                #region 解压Natives Natives Decompresser

                try
                {
                    if (!Directory.Exists(argumentParser.NativeRoot))
                        Directory.CreateDirectory(argumentParser.NativeRoot);

                    //TODO
                    DirectoryHelper.CleanDirectory(argumentParser.NativeRoot);
                    version.Natives.ForEach(n =>
                    {
                        var path =
                            Path.Combine(RootPath, GamePathHelper.GetLibraryPath(string.Empty),
                                n.FileInfo.Path.Replace('/', '\\'));
                        using var stream =
                            File.OpenRead(path);
                        using var reader = ReaderFactory.Open(stream);
                        while (reader.MoveToNextEntry())
                            if (!(n.Extract?.Exclude?.Contains(reader.Entry.Key) ?? false))
                                reader.WriteEntryToDirectory(Path.Combine(RootPath, argumentParser.NativeRoot),
                                    new ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                    });
                }
                catch (Exception e)
                {
                    return new LaunchResult
                    {
                        Error = new ErrorModel
                        {
                            Exception = e
                        },
                        ErrorType = LaunchErrorType.DecompressFailed,
                        LaunchSettings = settings,
                        RunTime = prevSpan
                    };
                }

                InvokeLaunchLogThenStart("解压Natives", ref prevSpan, ref stopwatch);

                #endregion

                #region 启动游戏 Launch

                var launchWrapper = new LaunchWrapper(authResult)
                {
                    GameCore = this,
                    Process = Process.Start(new ProcessStartInfo(executable, sb.ToString())
                    {
                        UseShellExecute = false,
                        WorkingDirectory = RootPath,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    })
                };
                launchWrapper.Do();
                InvokeLaunchLogThenStart("启动游戏", ref prevSpan, ref stopwatch);

                //绑定游戏退出事件。
                //Bind the exit event.
                new Thread(async () =>
                {
                    await Task.Run(launchWrapper.Process.WaitForExit).ContinueWith(task =>
                                GameExit(launchWrapper, new GameExitEventArgs
                                {
                                    Exception = task.Exception,
                                    ExitCode = launchWrapper.ExitCode
                                }),
                            CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default)
                        .ConfigureAwait(false);
                }).Start();

                if (!string.IsNullOrEmpty(settings.WindowTitle))
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                    Task.Run(() =>
                    {
                        while (string.IsNullOrEmpty(launchWrapper.Process.MainWindowTitle))
                            _ = NativeMethods.SetWindowText(launchWrapper.Process.MainWindowHandle,
                                settings.WindowTitle);
                    });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

                #endregion

                //返回启动结果。
                //Return the launch result.
                return new LaunchResult
                {
                    RunTime = stopwatch.Elapsed,
                    GameProcess = launchWrapper.Process,
                    LaunchSettings = settings
                };
            }
            catch (Exception ex)
            {
                return new LaunchResult
                {
                    LaunchSettings = settings,
                    Error = new ErrorModel
                    {
                        Exception = ex
                    }
                };
            }
        }

        #region IDisposable Support

        /// <summary>
        ///     释放资源。
        /// </summary>
        public void Dispose()
        {
        }

        #endregion

        #region 内部方法 Internal Methods

        /// <summary>
        ///     （内部方法）写入日志，记录时间。
        ///     Write the log and record the time.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="time"></param>
        /// <param name="sw"></param>
        private void InvokeLaunchLogThenStart(string item, ref TimeSpan time, ref Stopwatch sw)
        {
            LogLaunchData(this, new LaunchLogEventArgs
            {
                Item = item,
                ItemRunTime = sw.Elapsed - time
            });
            time = sw.Elapsed;
            sw.Start();
        }

        /// <summary>
        ///     指示需要记录游戏日志。
        ///     此方法将引发事件 <seealso cref="GameLogEventDelegate" /> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void LogGameData(object sender, GameLogEventArgs e)
        {
            GameLogEventDelegate?.Invoke(sender, e);
        }

        /// <summary>
        ///     指示游戏已经结束。
        ///     此方法将引发事件 <seealso cref="GameLogEventDelegate" /> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void GameExit(object sender, GameExitEventArgs e)
        {
            GameExitEventDelegate?.Invoke(sender, e);
        }

        /// <summary>
        ///     指示需要记录启动日志。
        ///     此方法将引发事件 <seealso cref="GameLogEventDelegate" /> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void LogLaunchData(object sender, LaunchLogEventArgs e)
        {
            LaunchLogEventDelegate?.Invoke(sender, e);
        }

        #endregion
    }
}