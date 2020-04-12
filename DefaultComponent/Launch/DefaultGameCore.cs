using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Authenticator;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ProjBobcat.DefaultComponent.Launch
{
    /// <summary>
    /// 表示一个默认的游戏核心。
    /// </summary>
    public class DefaultGameCore : IGameCore, IDisposable
    {
        private string _rootPath;
        /// <summary>
        /// 获取或设置根目录。
        /// </summary>
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
        /// <summary>
        /// 获取或设置版本定位器。
        /// </summary>
        public IVersionLocator VersionLocator { get; set; }
        /// <summary>
        /// 获取或设置客户端令牌。
        /// </summary>
        public Guid ClientToken { get; set; }
        /// <summary>
        /// 在游戏退出时触发。
        /// </summary>
        public event EventHandler<GameExitEventArgs> GameExitEventDelegate;
        /// <summary>
        /// 在需要记录游戏日志时触发。
        /// </summary>
        public event EventHandler<GameLogEventArgs> GameLogEventDelegate;
        /// <summary>
        /// 在需要记录启动日志时触发。
        /// </summary>
        public event EventHandler<LaunchLogEventArgs> LaunchLogEventDelegate;
        /// <summary>
        /// 启动游戏。
        /// 若启动成功，其返回值会包含消耗的时间；失败则包含异常信息。
        /// </summary>
        /// <param name="settings">启动设置。</param>
        /// <returns>启动结果。若启动成功，会包含消耗的时间；失败则包含异常信息。</returns>
        [Obsolete("此方法已过时，请使用其对应的异步方法 AuthTaskAsync(bool)", true)]
        public LaunchResult Launch(LaunchSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 启动游戏。
        /// 若启动成功，其返回值会包含消耗的时间；失败则包含异常信息。
        /// </summary>
        /// <param name="settings">启动设置。</param>
        /// <returns>启动结果。若启动成功，会包含消耗的时间；失败则包含异常信息。</returns>
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
                    OfflineAuthenticator off => off.Auth(false),
                    YggdrasilAuthenticator ygg => await ygg.AuthTaskAsync(true).ConfigureAwait(true),
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

                #endregion

                #region 解析启动参数 Launch Parameters Resolver

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
                arguments.ForEach(arg => sb.Append(arg.Trim()).Append(" "));
                InvokeLaunchLogThenStart(sb.ToString(), ref prevSpan, ref stopwatch);

                #endregion

                #region 解压Natives Natives Decompresser

                try
                {
                    if (!Directory.Exists(argumentParser.NativeRoot))
                        Directory.CreateDirectory(argumentParser.NativeRoot);

                    //TODO
                    DirectoryHelper.CleanDirectory(argumentParser.NativeRoot, false);
                    version.Natives.ForEach(n =>
                    {
                        var path =
                            $"{GamePathHelper.GetLibraryPath(RootPath.TrimEnd('\\'), string.Empty)}\\{n.FileInfo.Path.Replace('/', '\\')}";
                        using var stream =
                            File.OpenRead(path);
                        using var reader = ReaderFactory.Open(stream);
                        while (reader.MoveToNextEntry())
                            if (!(n.Extract?.Exclude?.Contains(reader.Entry.Key) ?? false))
                                reader.WriteEntryToDirectory(argumentParser.NativeRoot,
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
                        WorkingDirectory = settings.GameResourcePath,
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

                #endregion

                //返回启动结果。
                //Return the launch result.
                return new LaunchResult
                {
                    RunTime = stopwatch.Elapsed
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
        /// 指示需要记录游戏日志。
        /// 此方法将引发事件 <seealso cref="GameLogEventDelegate"/> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void LogGameData(object sender, GameLogEventArgs e)
        {
            GameLogEventDelegate?.Invoke(sender, e);
        }

        /// <summary>
        /// 指示游戏已经结束。
        /// 此方法将引发事件 <seealso cref="GameLogEventDelegate"/> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void GameExit(object sender, GameExitEventArgs e)
        {
            GameExitEventDelegate?.Invoke(sender, e);
        }

        /// <summary>
        /// 指示需要记录启动日志。
        /// 此方法将引发事件 <seealso cref="GameLogEventDelegate"/> 。
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        public void LogLaunchData(object sender, LaunchLogEventArgs e)
        {
            LaunchLogEventDelegate?.Invoke(sender, e);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            VersionLocator = null;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        ~DefaultGameCore()
        {
            VersionLocator = null;
        }
        #endregion
    }
}