using Microsoft.Extensions.Configuration;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.Event;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using FileInfo = System.IO.FileInfo;

namespace ProjBobcat.DefaultComponent.Launch.GameCore;

/// <summary>
///     表示一个默认的游戏核心。
/// </summary>
public sealed partial class DefaultGameCore : GameCoreBase
{
    readonly string _rootPath = null!;

    /// <summary>
    ///     .minecraft 目录
    /// </summary>
    public override required string RootPath
    {
        get => this._rootPath;
        init
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            this._rootPath = Path.GetFullPath(value.TrimEnd('/'));
        }
    }

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex CrLfRegex();

    private void CleanupOldNatives(LaunchSettings settings)
    {
        var nativeRoot = Path.Combine(this.RootPath, GamePathHelper.GetNativeRoot(settings.Version));
        var di = new DirectoryInfo(nativeRoot);

        if (!di.Exists) return;

        var dirs = di.EnumerateDirectories()
            .Where(d => (DateTime.Now - d.CreationTime).TotalDays >= 1)
            .ToFrozenSet();

        foreach (var dir in dirs)
            try
            {
                DirectoryHelper.CleanDirectory(dir.FullName, true);
            }
            catch (IOException)
            {
                // Ignore
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore
            }
    }

    private static async Task<ProcessStartInfo> StartGameInShellInWindows(
        string rootPath,
        string command,
        bool preferUtf8Encoding)
    {
        var ansi = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        var tempScriptPath = Path.Combine(Path.GetTempPath(), "launch_java_command.bat");
        var fileContent = $"""
                           chcp {(preferUtf8Encoding ? 65001 : ansi)}
                           @echo off
                           cd /d "{rootPath}"
                           {command}
                           pause
                           """;

        fileContent = CrLfRegex().Replace(fileContent, "\r\n");

        var encoding = preferUtf8Encoding
            ? Encoding.UTF8
            : Encoding.GetEncoding(ansi);

        await File.WriteAllTextAsync(tempScriptPath, fileContent, encoding);

        return new ProcessStartInfo("cmd.exe", $"/k \"{tempScriptPath}\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
            WorkingDirectory = rootPath
        };
    }

    private static async Task<ProcessStartInfo> StartGameInShellInUnix(
        string rootPath,
        string command)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), "launch_java_command.sh");
        await File.WriteAllTextAsync(tempScriptPath, $"""
                                                      #!/bin/bash
                                                      cd "{rootPath}"
                                                      {command}
                                                      exec bash
                                                      """);
        var chmodProcess = Process.Start(new ProcessStartInfo("chmod", $"+x \"{tempScriptPath}\"")
            { UseShellExecute = false });

        ArgumentNullException.ThrowIfNull(chmodProcess);

        await chmodProcess.WaitForExitAsync();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new ProcessStartInfo("open", $"-a Terminal \"{tempScriptPath}\"")
            {
                UseShellExecute = true
            };

        var terminal = File.Exists("/usr/bin/gnome-terminal") ? "gnome-terminal" : "xterm";
        if (terminal == "gnome-terminal")
            return new ProcessStartInfo(terminal, $"-- bash -c '{tempScriptPath}; exec bash'")
            {
                UseShellExecute = true,
                WorkingDirectory = rootPath
            };

        return new ProcessStartInfo(terminal, $"-e bash -c '{tempScriptPath}; exec bash'")
        {
            UseShellExecute = true,
            WorkingDirectory = rootPath
        };
    }

    private static IReadOnlyDictionary<string, string> ParseGameEnv(string[] lines)
    {
        var result = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var firstEqualIndex = line.IndexOf('=');

            if (firstEqualIndex == -1) continue;

            var key = line[..firstEqualIndex].Trim();
            var value = line[(firstEqualIndex + 1)..].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;

            result[key] = value;
        }

        return result;
    }

    public override async Task<LaunchResult> LaunchTaskAsync(LaunchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(this.VersionLocator.LauncherProfileParser);

        try
        {
            //逐步测量启动时间。
            //Measure the launch time step by step.
            var currentTimestamp = Stopwatch.GetTimestamp();

            #region 解析游戏 Game Info Resolver

            var tempVersion = this.VersionLocator.GetGame(settings.Version);

            if (tempVersion is BrokenVersionInfo brokenVersion)
                return new LaunchResult
                {
                    ErrorType = LaunchErrorType.OperationFailed,
                    Error = new ErrorModel
                    {
                        Error = "解析游戏失败",
                        ErrorMessage = "我们在解析游戏时出现了错误",
                        Cause = $"[{brokenVersion.BrokenReason}] 这有可能是因为您的游戏JSON文件损坏所导致的问题"
                    }
                };

            var version = (VersionInfo)tempVersion;

            //在以下方法中，我们存储前一个步骤的时间并且重置秒表，以此逐步测量启动时间。
            //In the method InvokeLaunchLogThenStart(args), we storage the time span of the previous process and restart the watch in order that the time used in each step is recorded.
            this.InvokeLaunchLogThenStart(settings.Version, "解析游戏", ref currentTimestamp);

            #endregion

            #region 验证账户凭据 Legal Account Verifier

            this.InvokeLaunchLogThenStart(settings.Version, "正在验证账户凭据", ref currentTimestamp);

            //以下代码实现了账户模式从离线到在线的切换。
            //The following code switches account mode between offline and yggdrasil.
            var authResult = settings.Authenticator switch
            {
                OfflineAuthenticator off => off.Auth(),
                YggdrasilAuthenticator ygg => await ygg.AuthTaskAsync(true),
                MicrosoftAuthenticator mic => await mic.AuthTaskAsync(),
                _ => null
            };

            this.InvokeLaunchLogThenStart(settings.Version, "账户凭据验证完成", ref currentTimestamp);

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

            if (authResult.SelectedProfile == null && settings.SelectedProfile == null)
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

            if (settings.SelectedProfile != null)
                authResult.SelectedProfile = settings.SelectedProfile;

            #endregion

            #region 解析启动参数 Launch Parameters Resolver

            var java = settings.GameArguments.Java ?? settings.FallBackGameArguments?.Java;
            var isJavaExists = !string.IsNullOrWhiteSpace(java?.JavaPath);

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
                this.VersionLocator.LauncherProfileParser,
                this.VersionLocator,
                this.RootPath);

            //以字符串数组形式生成启动参数。
            //Generates launch cmd arguments in string[].
            var resolvedVersion = settings.VersionLocator.ResolveGame(
                (VersionInfo)tempVersion,
                settings.NativeReplacementPolicy,
                java);

            if (resolvedVersion == null)
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

            var nativeRoot = Path.Combine(this.RootPath, GamePathHelper.CreateNativeRoot(settings.Version));
            var arguments =
                argumentParser.GenerateLaunchArguments(nativeRoot, version, resolvedVersion, settings, authResult);
            this.InvokeLaunchLogThenStart(settings.Version, "解析启动参数", ref currentTimestamp);

            //通过String Builder格式化参数。（转化成字符串）
            //Format the arguments using string builder.(Convert to string)
            // arguments.ForEach(arg => sb.Append(arg.Trim()).Append(' '));
            this.InvokeLaunchLogThenStart(settings.Version, string.Join(Environment.NewLine, arguments),
                ref currentTimestamp);

            #endregion

            #region 解压Natives Natives Decompresser

            try
            {
                var nativeRootPath = Path.Combine(this.RootPath, nativeRoot);

                if (!Directory.Exists(nativeRootPath))
                    Directory.CreateDirectory(nativeRootPath);

                DirectoryHelper.CleanDirectory(nativeRootPath);
                this.CleanupOldNatives(settings);

                foreach (var n in resolvedVersion.Natives)
                {
                    var path =
                        Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(n.FileInfo.Path!));

                    if (!File.Exists(path)) continue;

                    await using var stream = File.OpenRead(path);
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (n.Extract?.Exclude != null &&
                            n.Extract.Exclude.Any(e => entry.FullName.StartsWith(e, StringComparison.Ordinal)))
                            continue;

                        var extractPath = Path.Combine(nativeRootPath, entry.FullName);
                        if (entry.IsDirectory())
                        {
                            if (!Directory.Exists(extractPath))
                                Directory.CreateDirectory(extractPath);

                            continue;
                        }

                        this.InvokeLaunchLogThenStart(settings.Version, $"[解压 Natives] - {entry.FullName}",
                            ref currentTimestamp);

                        var fi = new FileInfo(extractPath);
                        var di = fi.Directory ?? new DirectoryInfo(Path.GetDirectoryName(extractPath)!);

                        if (!di.Exists)
                            di.Create();

                        await using var fs = fi.OpenWrite();
                        await using var entryStream = entry.Open();

                        await entryStream.CopyToAsync(fs);
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

            ProcessStartInfo psi;

            if (!settings.UseShellExecute)
            {
                var ansi = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                var encoding = settings.PreferUtf8Encoding
                    ? Encoding.UTF8
                    : Encoding.GetEncoding(ansi);

                psi = new ProcessStartInfo(java!.JavaPath, string.Join(' ', arguments))
                {
                    UseShellExecute = false,
                    WorkingDirectory = rootPath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = encoding,
                    StandardErrorEncoding = encoding
                };
            }
            else
            {
                var normalJavaPath = java!.JavaPath.Replace("javaw", "java", StringComparison.OrdinalIgnoreCase);
                var javaCommand = $"\"{normalJavaPath}\" {string.Join(' ', arguments)}";

                psi = javaCommand switch
                {
                    _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) =>
                        await StartGameInShellInWindows(rootPath, javaCommand, settings.PreferUtf8Encoding),
                    _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                           RuntimeInformation.IsOSPlatform(OSPlatform.Linux) =>
                        await StartGameInShellInUnix(rootPath, javaCommand),
                    _ => throw new PlatformNotSupportedException("Unsupported OS platform.")
                };
            }

            if (!settings.UseShellExecute)
            {
                // Patch for third-party launcher
                psi.EnvironmentVariables.Remove("JAVA_TOOL_OPTIONS");

                foreach (var (k, v) in ParseGameEnv(settings.GameEnvironmentVariables))
                {
                    psi.EnvironmentVariables.Add(k, v);
                }
            }

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

            this.InvokeLaunchLogThenStart(settings.Version, "设置 log4j 缓解措施", ref currentTimestamp);

            #endregion

            // arguments.ForEach(psi.ArgumentList.Add);

            var launchWrapper = new LaunchWrapper(authResult, settings)
            {
                GameCore = this,
                Process = Process.Start(psi)
            };

            launchWrapper.Do();
            this.InvokeLaunchLogThenStart(settings.Version, "启动游戏", ref currentTimestamp);

            if (launchWrapper.Process == null)
            {
                this.OnGameExit(launchWrapper, new GameExitEventArgs
                {
                    SourceGameId = settings.Version,
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
                        SourceGameId = settings.Version,
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