using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.Extensions.Configuration;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Archives;
using FileInfo = System.IO.FileInfo;

namespace ProjBobcat.DefaultComponent.Launch.GameCore;

/// <summary>
///     表示一个默认的游戏核心。
/// </summary>
public sealed class DefaultGameCore : GameCoreBase
{
    readonly string _rootPath = null!;

    /// <summary>
    ///     启动参数解析器
    /// </summary>
    public IArgumentParser LaunchArgumentParser
    {
        get => throw new InvalidOperationException();
        set => throw new InvalidOperationException();
    }

    /// <summary>
    ///     .minecraft 目录
    /// </summary>
    public override required string RootPath
    {
        get => this._rootPath;
        init
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(this.RootPath));

            this._rootPath = Path.GetFullPath(value.TrimEnd('/'));
        }
    }

    public bool EnableXmlLoggingOutput { get; init; }

    public override async Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
    {
        if (this.VersionLocator.LauncherProfileParser == null)
            throw new ArgumentNullException(nameof(this.VersionLocator.LauncherProfileParser));

        try
        {
            //逐步测量启动时间。
            //Measure the launch time step by step.
            var currentTimestamp = Stopwatch.GetTimestamp();

            #region 解析游戏 Game Info Resolver

            var version = this.VersionLocator.GetGame(settings.Version);

            //在以下方法中，我们存储前一个步骤的时间并且重置秒表，以此逐步测量启动时间。
            //In the method InvokeLaunchLogThenStart(args), we storage the time span of the previous process and restart the watch in order that the time used in each step is recorded.
            this.InvokeLaunchLogThenStart("解析游戏", ref currentTimestamp);

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

            this.InvokeLaunchLogThenStart("正在验证账户凭据", ref currentTimestamp);

            //以下代码实现了账户模式从离线到在线的切换。
            //The following code switches account mode between offline and yggdrasil.
            var authResult = settings.Authenticator switch
            {
                OfflineAuthenticator off => off.Auth(),
                YggdrasilAuthenticator ygg => await ygg.AuthTaskAsync(true),
                MicrosoftAuthenticator mic => await mic.AuthTaskAsync(),
                _ => null
            };

            this.InvokeLaunchLogThenStart("账户凭据验证完成", ref currentTimestamp);

            //错误处理
            //Error processor
            if (authResult == null || authResult.AuthStatus == AuthStatus.Failed ||
                authResult.AuthStatus == AuthStatus.Unknown)
                return new LaunchResult
                {
                    LaunchSettings = settings,
                    ErrorType = LaunchErrorType.AuthFailed,
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
                    ErrorType = LaunchErrorType.OperationFailed,
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
                        Cause = "未找到JRE运行时，可能是输入的路径为空或出错，亦或是指定的文件并不存在。",
                        Error = "未找到JRE运行时",
                        ErrorMessage = "输入的路径为空或出错，亦或是指定的文件并不存在"
                    }
                };

            var argumentParser = new DefaultLaunchArgumentParser(
                settings, this.VersionLocator.LauncherProfileParser, this.VersionLocator,
                authResult, this.RootPath,
                version.RootVersion)
            {
                EnableXmlLoggingOutput = this.EnableXmlLoggingOutput
            };

            //以字符串数组形式生成启动参数。
            //Generates launch cmd arguments in string[].
            var arguments = argumentParser.GenerateLaunchArguments();
            this.InvokeLaunchLogThenStart("解析启动参数", ref currentTimestamp);

            if (string.IsNullOrEmpty(arguments.First()))
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
            //Load the first element(java's path) into the executable string and removes it from the generated arguments
            var executable = arguments[0];
            arguments.RemoveAt(0);

            //通过String Builder格式化参数。（转化成字符串）
            //Format the arguments using string builder.(Convert to string)
            // arguments.ForEach(arg => sb.Append(arg.Trim()).Append(' '));
            this.InvokeLaunchLogThenStart(string.Join(Environment.NewLine, arguments), ref currentTimestamp);

            #endregion

            #region 解压Natives Natives Decompresser

            try
            {
                var nativeRootPath = Path.Combine(this.RootPath, argumentParser.NativeRoot);
                if (!Directory.Exists(nativeRootPath))
                    Directory.CreateDirectory(nativeRootPath);

                DirectoryHelper.CleanDirectory(nativeRootPath);

                foreach (var n in version.Natives)
                {
                    var path =
                        Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(n.FileInfo.Path!));

                    if (!File.Exists(path)) continue;

                    // await using var stream = File.OpenRead(path);
                    // using var reader =  ReaderFactory.Open(stream);

                    using var archive = ArchiveFactory.Open(path);
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Key)) continue;
                        if (n.Extract?.Exclude?.Any(entry.Key.StartsWith) ?? false) continue;

                        var extractPath = Path.Combine(nativeRootPath, entry.Key);
                        if (entry.IsDirectory)
                        {
                            if (!Directory.Exists(extractPath))
                                Directory.CreateDirectory(extractPath);

                            continue;
                        }

                        this.InvokeLaunchLogThenStart($"[解压 Natives] - {entry.Key}", ref currentTimestamp);

                        var fi = new FileInfo(extractPath);
                        var di = fi.Directory ?? new DirectoryInfo(Path.GetDirectoryName(extractPath)!);

                        if (!di.Exists)
                            di.Create();

                        await using var fs = fi.OpenWrite();
                        entry.WriteTo(fs);
                    }
                }
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
                    RunTime = Stopwatch.GetElapsedTime(currentTimestamp)
                };
            }

            #endregion

            #region 启动游戏 Launch

            var rootPath = settings.VersionInsulation
                ? Path.Combine(this.RootPath, GamePathHelper.GetGamePath(settings.Version))
                : this.RootPath;

            var psi = new ProcessStartInfo(executable, string.Join(' ', arguments))
            {
                UseShellExecute = false,
                WorkingDirectory = rootPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            #region log4j 缓解措施

            const string log4JKey = "FORMAT_MESSAGES_PATTERN_DISABLE_LOOKUPS";
            const string log4JValue = "true";

            await Task.Run(() =>
            {
                try
                {
                    var configuration = new ConfigurationBuilder()
                        .AddEnvironmentVariables()
                        .Build();

                    if (configuration[log4JKey] == log4JValue) return;

                    Environment.SetEnvironmentVariable(log4JKey, log4JValue,
                        EnvironmentVariableTarget.User);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            this.InvokeLaunchLogThenStart("设置 log4j 缓解措施", ref currentTimestamp);

            #endregion

            // arguments.ForEach(psi.ArgumentList.Add);

            var launchWrapper = new LaunchWrapper(authResult, settings)
            {
                GameCore = this,
                Process = Process.Start(psi)
            };

            launchWrapper.Do();
            this.InvokeLaunchLogThenStart("启动游戏", ref currentTimestamp);

            if (launchWrapper.Process == null)
            {
                this.OnGameExit(launchWrapper, new GameExitEventArgs
                {
                    Exception = null,
                    ExitCode = -1
                });

                return new LaunchResult
                {
                    RunTime = Stopwatch.GetElapsedTime(currentTimestamp),
                    LaunchSettings = settings
                };
            }

            //绑定游戏退出事件。
            //Bind the exit event.
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            Task.Run(launchWrapper.Process.WaitForExit)
                .ContinueWith(task =>
                {
                    this.OnGameExit(launchWrapper, new GameExitEventArgs
                    {
                        Exception = task.Exception,
                        ExitCode = launchWrapper.ExitCode == 0
                            ? ProcessorHelper.TryGetProcessExitCode(launchWrapper.Process, out var code) ? code : 0
                            : launchWrapper.ExitCode
                    });
                });

            if (!string.IsNullOrEmpty(settings.WindowTitle))
                Task.Run(() =>
                {
                    do
                    {
                        if (launchWrapper.Process == null) break;

                        if (OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(5))
                            _ = PInvoke.SetWindowText(
                                new HWND(launchWrapper.Process.MainWindowHandle),
                                settings.WindowTitle);
                    } while (string.IsNullOrEmpty(launchWrapper.Process?.MainWindowTitle));
                });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            #endregion

            //返回启动结果。
            //Return the launch result.
            return new LaunchResult
            {
                RunTime = Stopwatch.GetElapsedTime(currentTimestamp),
                GameProcess = launchWrapper.Process,
                LaunchSettings = settings
            };
        }
        catch (Exception ex)
        {
            return new LaunchResult
            {
                LaunchSettings = settings,
                ErrorType = LaunchErrorType.OperationFailed,
                Error = new ErrorModel
                {
                    Exception = ex
                }
            };
        }
    }
}