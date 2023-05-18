using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller;

public partial class HighVersionForgeInstaller : InstallerBase, IForgeInstaller
{
#if NET7_0_OR_GREATER
    [GeneratedRegex("^\\[.+\\]$")]
    private static partial Regex PathRegex();

    [GeneratedRegex("^{.+}$")]
    private static partial Regex VariableRegex();

#else

    static readonly Regex
        PathRegex = new("^\\[.+\\]$", RegexOptions.Compiled),
        VariableRegex = new("^{.+}$", RegexOptions.Compiled);

#endif

    readonly ConcurrentBag<DownloadFile> _failedFiles = new();
    int _totalDownloaded, _needToDownload, _totalProcessed, _needToProcess;

    public string JavaExecutablePath { get; init; }

    public string MineCraftVersionId { get; set; }
    public string MineCraftVersion { get; set; }

    public string DownloadUrlRoot { get; set; }
    public string ForgeExecutablePath { get; set; }

    public VersionLocatorBase VersionLocator { get; set; }

    public ForgeInstallResult InstallForge()
    {
        return InstallForgeTaskAsync().Result;
    }

    public async Task<ForgeInstallResult> InstallForgeTaskAsync()
    {
        if (string.IsNullOrEmpty(ForgeExecutablePath))
            throw new ArgumentNullException("未指定\"ForgeExecutablePath\"参数");
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

        using var archive = ArchiveFactory.Open(Path.GetFullPath(ForgeExecutablePath));

        #region 解析 Version.json

        InvokeStatusChangedEvent("解析 Version.json", 0.1);

        var versionJsonEntry =
            archive.Entries.FirstOrDefault(e => e.Key.Equals("version.json", StringComparison.OrdinalIgnoreCase));

        if (versionJsonEntry == default)
            return new ForgeInstallResult
            {
                Succeeded = false,
                Error = new ErrorModel
                {
                    Cause = "损坏的 Forge 可执行文件",
                    Error = "安装前准备失败",
                    ErrorMessage = "损坏的 Forge 可执行文件，请确认您的路径是否正确"
                }
            };

        await using var stream = versionJsonEntry.OpenEntryStream();
        var versionJsonModel =
            await JsonSerializer.DeserializeAsync(stream, RawVersionModelContext.Default.RawVersionModel);

        var forgeVersion = versionJsonModel.Id.Replace("-forge-", "-");
        var id = string.IsNullOrEmpty(CustomId) ? versionJsonModel.Id : CustomId;

        versionJsonModel.Id = id;
        if (!string.IsNullOrEmpty(InheritsFrom))
            versionJsonModel.InheritsFrom = InheritsFrom;

        var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonContent = JsonSerializer.Serialize(versionJsonModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        #endregion

        #region 解析 Install_profile.json

        InvokeStatusChangedEvent("解析 Install_profile.json", 0.2);

        var installProfileEntry =
            archive.Entries.FirstOrDefault(e =>
                e.Key.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase));

        await using var ipStream = installProfileEntry.OpenEntryStream();
        var ipModel =
            await JsonSerializer.DeserializeAsync(ipStream, ForgeInstallProfileContext.Default.ForgeInstallProfile);

        #endregion

        #region 解析 Lzma

        InvokeStatusChangedEvent("解析 Lzma", 0.4);

        var serverLzma = archive.Entries.FirstOrDefault(e =>
            e.Key.Equals("data/server.lzma", StringComparison.OrdinalIgnoreCase));
        var clientLzma = archive.Entries.FirstOrDefault(e =>
            e.Key.Equals("data/client.lzma", StringComparison.OrdinalIgnoreCase));

        if (serverLzma != default)
        {
            var serverMaven = $"net.minecraftforge:forge:{forgeVersion}:serverdata@lzma";

            ipModel.Data["BINPATCH"].Server = $"[{serverMaven}]";

            var serverBinMaven = serverMaven.ResolveMavenString();
            var serverBinPath = Path.Combine(RootPath,
                GamePathHelper.GetLibraryPath(serverBinMaven.Path));

            var di = new DirectoryInfo(Path.GetDirectoryName(serverBinPath));

            if (!di.Exists)
                di.Create();

            await using var sFs = File.OpenWrite(serverBinPath);

            serverLzma.WriteTo(sFs);
        }

        if (clientLzma != default)
        {
            var clientMaven = $"net.minecraftforge:forge:{forgeVersion}:clientdata@lzma";

            ipModel.Data["BINPATCH"].Client = $"[{clientMaven}]";

            var clientBinMaven = clientMaven.ResolveMavenString();
            var clientBinPath = Path.Combine(RootPath,
                GamePathHelper.GetLibraryPath(clientBinMaven.Path));

            var di = new DirectoryInfo(Path.GetDirectoryName(clientBinPath));

            if (!di.Exists)
                di.Create();

            await using var cFs = File.OpenWrite(clientBinPath);
            clientLzma.WriteTo(cFs);
        }

        #endregion

        #region 解压 Forge Jar

        InvokeStatusChangedEvent("解压 Forge Jar", 0.5);

        var forgeJar = archive.Entries.FirstOrDefault(e =>
            e.Key.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}.jar",
                StringComparison.OrdinalIgnoreCase));
        var forgeUniversalJar = archive.Entries.FirstOrDefault(e =>
            e.Key.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-universal.jar",
                StringComparison.OrdinalIgnoreCase));

        if (forgeJar != default)
        {
            if (forgeUniversalJar != default)
            {
                var forgeUniversalSubPath = forgeUniversalJar?.Key[(forgeUniversalJar.Key.IndexOf('/') + 1)..];
                var forgeUniversalLibPath = Path.Combine(RootPath,
                    GamePathHelper.GetLibraryPath(forgeUniversalSubPath));

                if (string.IsNullOrEmpty(forgeUniversalSubPath)
                    || string.IsNullOrEmpty(forgeUniversalLibPath))
                    return new ForgeInstallResult
                    {
                        Error = new ErrorModel
                        {
                            ErrorMessage = "不支持的格式"
                        },
                        Succeeded = false
                    };

                var forgeUniversalLibDir = Path.GetDirectoryName(forgeUniversalLibPath);
                if (!Directory.Exists(forgeUniversalLibDir))
                    Directory.CreateDirectory(forgeUniversalLibDir);

                await using var forgeUniversalFs = File.OpenWrite(forgeUniversalLibPath);
                forgeUniversalJar.WriteTo(forgeUniversalFs);
            }

            var forgeSubPath = forgeJar.Key[(forgeJar.Key.IndexOf('/') + 1)..];
            var forgeLibPath =
                Path.Combine(RootPath, GamePathHelper.GetLibraryPath(forgeSubPath));

            var forgeLibDir = Path.GetDirectoryName(forgeLibPath);
            if (!Directory.Exists(forgeLibDir))
                Directory.CreateDirectory(forgeLibDir);

            await using var forgeFs = File.OpenWrite(forgeLibPath);

            var fLDi = new DirectoryInfo(Path.GetDirectoryName(forgeLibPath));

            if (!fLDi.Exists)
                fLDi.Create();

            forgeJar.WriteTo(forgeFs);
        }

        #endregion

        #region 解析 Processor

        InvokeStatusChangedEvent("解析 Processor", 1);

        string ResolvePathRegex(string val)
        {
#if NET7_0_OR_GREATER
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(PathRegex().Match(val).Value)) return val;
#else
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(PathRegex.Match(val).Value)) return val;
#endif

            var name = val[1..^1];
            var maven = name.ResolveMavenString();
            var path = Path.Combine(RootPath,
                GamePathHelper.GetLibraryPath(maven.Path));

            return path;
        }

        var variables = new Dictionary<string, ForgeInstallProfileData>
        {
            {
                "MINECRAFT_JAR",
                new ForgeInstallProfileData
                {
                    Client = GamePathHelper.GetVersionJar(RootPath, MineCraftVersionId)
                }
            }
        };

        foreach (var (k, v) in ipModel.Data)
            variables.TryAdd(k, new ForgeInstallProfileData
            {
                Client = ResolvePathRegex(v.Client),
                Server = ResolvePathRegex(v.Server)
            });

        string ResolveVariableRegex(string val)
        {
#if NET7_0_OR_GREATER
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(VariableRegex().Match(val).Value)) return val;
#else
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(VariableRegex.Match(val).Value)) return val;
#endif

            var key = val[1..^1];
            return variables[key].Client;
        }

        var procList = new List<ForgeInstallProcessorModel>();
        var argsReplaceList = new Dictionary<string, string>
        {
            { "{SIDE}", "client" },
            { "{MINECRAFT_JAR}", GamePathHelper.GetVersionJar(RootPath, MineCraftVersionId) },
            { "{MINECRAFT_VERSION}", MineCraftVersion },
            { "{ROOT}", RootPath },
            { "{INSTALLER}", ForgeExecutablePath },
            { "{LIBRARY_DIR}", Path.Combine(RootPath, GamePathHelper.GetLibraryRootPath()) }
        };

        foreach (var proc in ipModel.Processors)
        {
            if (proc.Sides != null &&
                proc.Sides.Any() &&
                !proc.Sides.Any(s => s.Equals("client", StringComparison.OrdinalIgnoreCase))
               )
                continue;

            var outputs = new Dictionary<string, string>();

            if (proc.Outputs?.Any() ?? false)
                foreach (var (k, v) in proc.Outputs)
                    outputs.TryAdd(ResolveVariableRegex(k), ResolveVariableRegex(v));

            var args = proc.Arguments
                .Select(arg => StringHelper.ReplaceByDic(arg, argsReplaceList))
                .Select(ResolvePathRegex)
                .Select(ResolveVariableRegex)
                .Select(StringHelper.FixPathArgument)
                .ToArray();
            var model = new ForgeInstallProcessorModel
            {
                Processor = proc,
                Arguments = args,
                Outputs = outputs
            };

            procList.Add(model);
        }

        #endregion

        #region 补全 Libraries

        _failedFiles.Clear();

        var libs = ipModel.Libraries.ToList();
        libs.AddRange(versionJsonModel.Libraries);

        var resolvedLibs = VersionLocator.GetNatives(libs).Item2;
        var libDownloadInfo = new List<DownloadFile>();

        foreach (var lib in resolvedLibs)
        {
            if (
                lib.Name.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(lib.Url)
            )
                continue;

            var symbolIndex = lib.Path.LastIndexOf('/');
            var fileName = lib.Path[(symbolIndex + 1)..];
            var path = Path.Combine(RootPath,
                GamePathHelper.GetLibraryPath(lib.Path[..symbolIndex]));

            /*
            if (!string.IsNullOrEmpty(DownloadUrlRoot))
            {
                var urlRoot = HttpHelper.RegexMatchUri(lib.Url);
                var url = lib.Url.Replace($"{urlRoot}/", string.Empty);
                if (!url.StartsWith("maven", StringComparison.OrdinalIgnoreCase))
                    url = "maven/" + url;

                lib.Url = $"{DownloadUrlRoot}{url}";
            }
            */

            var libDi = new DirectoryInfo(path);

            if (!libDi.Exists)
                libDi.Create();

            var df = new DownloadFile
            {
                CheckSum = lib.Sha1,
                DownloadPath = path,
                FileName = fileName,
                DownloadUri = lib.Url,
                FileSize = lib.Size
            };
            df.Completed += WhenCompleted;

            libDownloadInfo.Add(df);
        }

        _needToDownload = libDownloadInfo.Count;

        await DownloadHelper.AdvancedDownloadListFile(libDownloadInfo, new DownloadSettings
        {
            CheckFile = true,
            DownloadParts = 4,
            HashType = HashType.SHA1,
            RetryCount = 3,
            Timeout = 5000
        });

        if (!_failedFiles.IsEmpty)
            return new ForgeInstallResult
            {
                Succeeded = false,
                Error = new ErrorModel
                {
                    Cause = "未能下载全部依赖",
                    Error = "未能下载全部依赖",
                    ErrorMessage = "未能下载全部依赖"
                }
            };

        #endregion

        #region 启动 Process

        _needToProcess = procList.Count;
        foreach (var processor in procList)
        {
            var maven = processor.Processor.Jar.ResolveMavenString();
            var libPath = Path.Combine(RootPath, GamePathHelper.GetLibraryPath(maven.Path));

            using var libArchive = ArchiveFactory.Open(Path.GetFullPath(libPath));
            var libEntry =
                libArchive.Entries.FirstOrDefault(e =>
                    e.Key.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase));

            await using var libStream = libEntry.OpenEntryStream();
            using var libSr = new StreamReader(libStream, Encoding.UTF8);
            var content = await libSr.ReadToEndAsync();
            var mainClass =
                (from line in content.Split('\n')
                    select line.Split(": ")
                    into lineSp
                    where lineSp[0].Equals("Main-Class", StringComparison.OrdinalIgnoreCase)
                    select lineSp[1].Trim()).First();

            var totalLibs = processor.Processor.ClassPath.ToList();
            totalLibs.Add(processor.Processor.Jar);

            var cp = totalLibs.Select(MavenHelper.ResolveMavenString)
                .Select(m => Path.Combine(RootPath, GamePathHelper.GetLibraryPath(m.Path)));
            var cpStr = string.Join(Path.PathSeparator, cp);
            var parameter = new List<string>
            {
                "-cp",
                $"\"{cpStr}\"",
                mainClass
            };

            parameter.AddRange(processor.Arguments);

            var pi = new ProcessStartInfo(JavaExecutablePath)
            {
                Arguments = string.Join(' ', parameter),
                UseShellExecute = false,
                WorkingDirectory = Path.GetFullPath(RootPath),
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var p = Process.Start(pi);

            if (p == null)
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Cause = "无法启动安装进程导致安装失败",
                        ErrorMessage = "安装过程中出现了错误"
                    },
                    Succeeded = false
                };

            var logSb = new StringBuilder();
            var errSb = new StringBuilder();

            p.OutputDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;

                logSb.AppendLine(args.Data);

                var data = args.Data ?? string.Empty;
                var progress = (double)_totalProcessed / _needToProcess;
                var dataLength = data.Length;
                var dataStr = dataLength > 30
                    ? $"..{data[(dataLength - 30)..]}"
                    : data;

                InvokeStatusChangedEvent($"{dataStr} <安装信息> ( {_totalProcessed} / {_needToProcess} )", progress);
            };

            p.ErrorDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;

                errSb.AppendLine(args.Data);

                var data = args.Data ?? string.Empty;
                var progress = (double)_totalProcessed / _needToProcess;
                var dataLength = data.Length;
                var dataStr = dataLength > 30
                    ? $"{data[(dataLength - 30)..]}"
                    : data;


                InvokeStatusChangedEvent($"{dataStr} <错误> ( {_totalProcessed} / {_needToProcess} )", progress);
            };

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            _totalProcessed++;
            await p.WaitForExitAsync();

            var installLogPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));

            if (logSb.Length != 0)
                await File.WriteAllTextAsync(Path.Combine(installLogPath, $"PROCESSOR #{_totalProcessed}_Logs.log"),
                    logSb.ToString());
            if (errSb.Length != 0)
                await File.WriteAllTextAsync(
                    Path.Combine(installLogPath, $"PROCESSOR #{_totalProcessed}_Errors.log"), errSb.ToString());

            if (errSb.Length != 0)
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Cause = "执行 Forge 安装脚本时出现了错误",
                        Error = errSb.ToString(),
                        ErrorMessage = "安装过程中出现了错误"
                    },
                    Succeeded = false
                };
        }

        #endregion

        return new ForgeInstallResult
        {
            Succeeded = true
        };
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not DownloadFile file) return;

        _totalDownloaded++;

        var progress = (double)_totalDownloaded / _needToDownload;
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;

        InvokeStatusChangedEvent(
            $"{retryStr}下载模组 - {file.FileName} ( {_totalDownloaded} / {_needToDownload} )",
            progress);

        if (!(e.Success ?? false)) _failedFiles.Add(file);
    }
}