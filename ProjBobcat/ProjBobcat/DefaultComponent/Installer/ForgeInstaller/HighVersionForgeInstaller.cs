using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller;

public partial class HighVersionForgeInstaller : InstallerBase, IForgeInstaller
{
    readonly ConcurrentBag<SimpleDownloadFile> _failedFiles = [];
    int _totalDownloaded, _needToDownload, _totalProcessed, _needToProcess;

    public required string JavaExecutablePath { get; init; }
    public required string MineCraftVersionId { get; init; }
    public required string MineCraftVersion { get; init; }

    public FileInfo? CustomMojangClientMappings { get; init; }
    public override required string RootPath { get; init; }
    public required string ForgeExecutablePath { get; init; }
    public required VersionLocatorBase VersionLocator { get; init; }

    public ForgeInstallResult InstallForge()
    {
        return this.InstallForgeTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<ForgeInstallResult> InstallForgeTaskAsync()
    {
        if (!File.Exists(this.JavaExecutablePath))
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

        if (!File.Exists(this.ForgeExecutablePath))
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

        await using var archiveFs = File.OpenRead(Path.GetFullPath(this.ForgeExecutablePath));
        using var archive = new ZipArchive(archiveFs, ZipArchiveMode.Read);

        #region 解析 Version.json

        this.InvokeStatusChangedEvent("解析 Version.json", ProgressValue.FromDisplay(10));

        var versionJsonEntry =
            archive.Entries.FirstOrDefault(e => e.FullName.Equals("version.json", StringComparison.OrdinalIgnoreCase));

        if (versionJsonEntry == null)
            return GetCorruptedFileResult();

        await using var stream = versionJsonEntry.Open();
        var versionJsonModel =
            await JsonSerializer.DeserializeAsync(stream, RawVersionModelContext.Default.RawVersionModel);

        if (versionJsonModel == null)
            return GetCorruptedFileResult();

        var forgeVersion = versionJsonModel.Id.Replace("-forge-", "-");
        var id = string.IsNullOrEmpty(this.CustomId) ? versionJsonModel.Id : this.CustomId;

        versionJsonModel.Id = id;
        if (!string.IsNullOrEmpty(this.InheritsFrom))
            versionJsonModel.InheritsFrom = this.InheritsFrom;

        var jsonPath = GamePathHelper.GetGameJsonPath(this.RootPath, id);
        var jsonContent = JsonSerializer.Serialize(versionJsonModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        #endregion

        #region 解析 Install_profile.json

        this.InvokeStatusChangedEvent("解析 Install_profile.json", ProgressValue.FromDisplay(20));

        var installProfileEntry =
            archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase));

        if (installProfileEntry == null)
            return GetCorruptedFileResult();

        await using var ipStream = installProfileEntry.Open();
        var ipModel =
            await JsonSerializer.DeserializeAsync(ipStream, ForgeInstallProfileContext.Default.ForgeInstallProfile);

        #endregion

        ArgumentNullException.ThrowIfNull(ipModel, "Forge install_profile is null, please check downloaded JAR!");

        #region 解析 Lzma

        this.InvokeStatusChangedEvent("解析 Lzma", ProgressValue.FromDisplay(40));

        var serverLzma = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals("data/server.lzma", StringComparison.OrdinalIgnoreCase));
        var clientLzma = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals("data/client.lzma", StringComparison.OrdinalIgnoreCase));

        if (serverLzma != null)
        {
            var serverMaven = $"net.minecraftforge:forge:{forgeVersion}:serverdata@lzma";

            ipModel.Data["BINPATCH"].Server = $"[{serverMaven}]";

            var serverBinMaven = serverMaven.ResolveMavenString()!;
            var serverBinPath = Path.Combine(this.RootPath,
                GamePathHelper.GetLibraryPath(serverBinMaven.Path));

            var di = new DirectoryInfo(Path.GetDirectoryName(serverBinPath)!);

            if (!di.Exists)
                di.Create();

            await using var sFs = File.OpenWrite(serverBinPath);
            await using var serverLzmaFs = serverLzma.Open();

            await serverLzmaFs.CopyToAsync(sFs);
        }

        if (clientLzma != null)
        {
            var clientMaven = $"net.minecraftforge:forge:{forgeVersion}:clientdata@lzma";

            ipModel.Data["BINPATCH"].Client = $"[{clientMaven}]";

            var clientBinMaven = clientMaven.ResolveMavenString()!;
            var clientBinPath = Path.Combine(this.RootPath,
                GamePathHelper.GetLibraryPath(clientBinMaven.Path));

            var di = new DirectoryInfo(Path.GetDirectoryName(clientBinPath)!);

            if (!di.Exists)
                di.Create();

            await using var cFs = File.OpenWrite(clientBinPath);
            await using var clientLzmaFs = clientLzma.Open();

            await clientLzmaFs.CopyToAsync(cFs);
        }

        #endregion

        #region 解压 Forge Jar

        this.InvokeStatusChangedEvent("解压 Forge Jar", ProgressValue.Finished);

        var forgeJar = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}.jar",
                StringComparison.OrdinalIgnoreCase));
        var forgeUniversalJar = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-universal.jar",
                StringComparison.OrdinalIgnoreCase));

        if (forgeJar != null)
        {
            if (forgeUniversalJar != null)
            {
                var forgeUniversalSubPath = forgeUniversalJar.FullName[(forgeUniversalJar.FullName.IndexOf('/') + 1)..];
                var forgeUniversalLibPath = Path.Combine(this.RootPath,
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

                var forgeUniversalLibDir = Path.GetDirectoryName(forgeUniversalLibPath)!;
                if (!Directory.Exists(forgeUniversalLibDir))
                    Directory.CreateDirectory(forgeUniversalLibDir);

                await using var forgeUniversalFs = File.OpenWrite(forgeUniversalLibPath);
                await using var forgeUniversalJarFs = forgeUniversalJar.Open();

                await forgeUniversalJarFs.CopyToAsync(forgeUniversalFs);
            }

            var forgeSubPath = forgeJar.FullName[(forgeJar.FullName.IndexOf('/') + 1)..];
            var forgeLibPath =
                Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(forgeSubPath));

            var forgeLibDir = Path.GetDirectoryName(forgeLibPath)!;
            if (!Directory.Exists(forgeLibDir))
                Directory.CreateDirectory(forgeLibDir);

            var fLDi = new DirectoryInfo(Path.GetDirectoryName(forgeLibPath)!);

            if (!fLDi.Exists)
                fLDi.Create();

            await using var forgeFs = File.OpenWrite(forgeLibPath);
            await using var forgeJarFs = forgeJar.Open();

            await forgeJarFs.CopyToAsync(forgeFs);
        }

        #endregion

        #region 预下载 Mojang Mappings

        var isMojMapDownloaded = false;

        if (ipModel.Data.TryGetValue("MOJMAPS", out var mapsVal) &&
            !string.IsNullOrEmpty(mapsVal.Client) && this.CustomMojangClientMappings != null &&
            !string.IsNullOrEmpty(this.CustomMojangClientMappings.Url))
        {
            this.InvokeStatusChangedEvent("预下载 MOJMAP...", ProgressValue.Start);

            var clientMavenStr = mapsVal.Client.TrimStart('[').TrimEnd(']');
            var resolvedMappingMaven = clientMavenStr.ResolveMavenString();
            
            if (resolvedMappingMaven == null)
                goto SkipMojMapDownload;

            var mavenDirName = Path.GetDirectoryName(resolvedMappingMaven.Path);

            if (string.IsNullOrEmpty(mavenDirName))
                goto SkipMojMapDownload;

            var mappingPath = Path.Combine(
                RootPath,
                GamePathHelper.GetLibraryRootPath(),
                mavenDirName);
            var mappingFileName = Path.GetFileName(resolvedMappingMaven.Path);
            var mappingDf = new SimpleDownloadFile
            {
                CheckSum = this.CustomMojangClientMappings.Sha1,
                DownloadPath = mappingPath,
                DownloadUri = this.CustomMojangClientMappings.Url,
                FileName = mappingFileName
            };

            mappingDf.Changed += (_, args) =>
            {
                this.InvokeStatusChangedEvent(
                    $"下载 - {mappingFileName} ( {args.ProgressPercentage} / 100 )",
                    args.ProgressPercentage);
            };

            mappingDf.Completed += (_, args) =>
            {
                if (args.Success)
                    isMojMapDownloaded = true;
            };

            if (!Directory.Exists(mappingPath))
                Directory.CreateDirectory(mappingPath);

            await DownloadHelper.DownloadData(mappingDf, new DownloadSettings
            {
                CheckFile = true,
                DownloadParts = 1,
                HashType = HashType.SHA1,
                RetryCount = 12,
                Timeout = TimeSpan.FromMinutes(1),
                HttpClientFactory = this.HttpClientFactory,
                ShowDownloadProgress = true
            });


        }

        #endregion

        SkipMojMapDownload:

        #region 解析 Processor

        this.InvokeStatusChangedEvent("解析 Processor", ProgressValue.Finished);

        string? ResolvePathRegex(string? val)
        {
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(PathRegex().Match(val).Value)) return val;

            var name = val[1..^1];
            var maven = name.ResolveMavenString()!;
            var path = Path.Combine(this.RootPath,
                GamePathHelper.GetLibraryPath(maven.Path));

            return path;
        }

        var variables = new Dictionary<string, ForgeInstallProfileData>
        {
            {
                "MINECRAFT_JAR",
                new ForgeInstallProfileData
                {
                    Client = GamePathHelper.GetVersionJar(this.RootPath, this.MineCraftVersionId)
                }
            }
        };

        foreach (var (k, v) in ipModel.Data)
        {
            var resolvedKey = ResolvePathRegex(v.Client);
            var resolvedValue = ResolvePathRegex(v.Server);

            if (string.IsNullOrEmpty(resolvedKey) ||
                string.IsNullOrEmpty(resolvedValue)) continue;

            variables.TryAdd(k, new ForgeInstallProfileData
            {
                Client = resolvedKey,
                Server = resolvedValue
            });
        }

        string? ResolveVariableRegex(string? val)
        {
            if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(VariableRegex().Match(val).Value)) return val;

            var key = val[1..^1];

            return variables[key].Client;
        }

        var procList = new List<ForgeInstallProcessorModel>();
        var argsReplaceList = new Dictionary<string, string>
        {
            { "{SIDE}", "client" },
            { "{MINECRAFT_JAR}", GamePathHelper.GetVersionJar(this.RootPath, this.MineCraftVersionId) },
            { "{MINECRAFT_VERSION}", this.MineCraftVersion },
            { "{ROOT}", this.RootPath },
            { "{INSTALLER}", this.ForgeExecutablePath },
            { "{LIBRARY_DIR}", Path.Combine(this.RootPath, GamePathHelper.GetLibraryRootPath()) }
        };

        foreach (var proc in ipModel.Processors)
        {
            if (proc.Sides is { Length: > 0 } &&
                !proc.Sides.Any(s => s.Equals("client", StringComparison.OrdinalIgnoreCase))
               )
                continue;

            var outputs = new Dictionary<string, string>();

            if ((proc.Outputs?.Count ?? 0) > 0)
                foreach (var (k, v) in proc.Outputs!)
                {
                    var resolvedKey = ResolveVariableRegex(k);
                    var resolvedValue = ResolveVariableRegex(v);

                    if (string.IsNullOrEmpty(resolvedKey) ||
                        string.IsNullOrEmpty(resolvedValue)) continue;

                    outputs.TryAdd(resolvedKey, resolvedValue);
                }

            if (isMojMapDownloaded &&
                proc.Arguments.Contains("DOWNLOAD_MOJMAPS"))
                continue;

            var args = proc.Arguments
                .Select(arg => StringHelper.ReplaceByDic(arg, argsReplaceList))
                .Select(ResolvePathRegex)
                .Select(ResolveVariableRegex)
                .Select(StringHelper.FixPathArgument)
                .OfType<string>()
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

        this._failedFiles.Clear();

        var resolvedLibs = this.VersionLocator.GetNatives([.. ipModel.Libraries, .. versionJsonModel.Libraries]).Item2;
        var libDownloadInfo = new List<SimpleDownloadFile>();

        foreach (var lib in resolvedLibs)
        {
            if (string.IsNullOrEmpty(lib.Path) ||
                string.IsNullOrEmpty(lib.Url)) continue;

            if (
                (lib.Name?.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase) ?? false) &&
                string.IsNullOrEmpty(lib.Url)
            )
                continue;

            var symbolIndex = lib.Path.LastIndexOf('/');
            var fileName = lib.Path[(symbolIndex + 1)..];
            var path = Path.Combine(this.RootPath,
                GamePathHelper.GetLibraryPath(lib.Path[..symbolIndex]));

            var fullFilePath = Path.Combine(path, fileName);
            if (File.Exists(fullFilePath))
                if (!string.IsNullOrEmpty(lib.Sha1))
                {
                    await using var fs = File.OpenRead(fullFilePath);
                    var hash = Convert.ToHexString(await SHA1.HashDataAsync(fs));

                    if (hash.Equals(lib.Sha1, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

            var libDi = new DirectoryInfo(path);

            if (!libDi.Exists)
                libDi.Create();

            var df = new SimpleDownloadFile
            {
                CheckSum = lib.Sha1,
                DownloadPath = path,
                FileName = fileName,
                DownloadUri = lib.Url,
                FileSize = lib.Size
            };
            df.Completed += this.WhenCompleted;

            libDownloadInfo.Add(df);
        }

        this._needToDownload = libDownloadInfo.Count;

        await DownloadHelper.AdvancedDownloadListFile(libDownloadInfo, new DownloadSettings
        {
            CheckFile = true,
            DownloadParts = 4,
            HashType = HashType.SHA1,
            RetryCount = 8,
            Timeout = TimeSpan.FromMinutes(5),
            HttpClientFactory = this.HttpClientFactory
        });

        if (!this._failedFiles.IsEmpty)
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

        this._needToProcess = procList.Count;
        foreach (var processor in procList)
        {
            var maven = processor.Processor.Jar.ResolveMavenString()!;
            var libPath = Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(maven.Path));

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            await using var libFs = await FileHelper.OpenReadAsync(Path.GetFullPath(libPath), cts.Token);

            ArgumentNullException.ThrowIfNull(libFs);

            using var libArchive = new ZipArchive(libFs, ZipArchiveMode.Read);

            var libEntry =
                libArchive.Entries.FirstOrDefault(e =>
                    e.FullName.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase));

            if (libEntry == null)
                return GetCorruptedFileResult();

            await using var libStream = libEntry.Open();
            using var libSr = new StreamReader(libStream, Encoding.UTF8);
            var content = await libSr.ReadToEndAsync(cts.Token);
            var mainClass =
                (from line in content.Split('\n')
                    select line.Split(": ")
                    into lineSp
                    where lineSp[0].Equals("Main-Class", StringComparison.OrdinalIgnoreCase)
                    select lineSp[1].Trim()).First();

            var totalLibs = processor.Processor.ClassPath.ToList();
            totalLibs.Add(processor.Processor.Jar);

            var cp = totalLibs
                .Select(MavenHelper.ResolveMavenString)
                .Select(m => Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(m!.Path)));
            var cpStr = string.Join(Path.PathSeparator, cp);
            var parameter = new List<string>
            {
                "-cp",
                $"\"{cpStr}\"",
                mainClass
            };

            parameter.AddRange(processor.Arguments);

            var pi = new ProcessStartInfo(this.JavaExecutablePath)
            {
                Arguments = string.Join(' ', parameter),
                UseShellExecute = false,
                WorkingDirectory = Path.GetFullPath(this.RootPath),
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

                var data = args.Data;

                var progress = ProgressValue.Create(this._totalProcessed, this._needToProcess);
                var dataStr = data.CropStr(40);

                this.InvokeStatusChangedEvent($"{dataStr} <安装信息> ( {this._totalProcessed} / {this._needToProcess} )",
                    progress);
            };

            p.ErrorDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;

                errSb.AppendLine(args.Data);

                var data = args.Data ?? string.Empty;
                var progress = ProgressValue.Create(this._totalProcessed, this._needToProcess);
                var dataStr = data.CropStr(40);

                this.InvokeStatusChangedEvent($"{dataStr} <错误> ( {this._totalProcessed} / {this._needToProcess} )",
                    progress);
            };

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            this._totalProcessed++;
            await p.WaitForExitAsync(cts.Token);

            var installLogPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));

            if (logSb.Length != 0)
                await File.WriteAllTextAsync(
                    Path.Combine(installLogPath, $"PROCESSOR #{this._totalProcessed}_Logs.log"),
                    logSb.ToString(), cts.Token);
            if (errSb.Length != 0)
                await File.WriteAllTextAsync(
                    Path.Combine(installLogPath, $"PROCESSOR #{this._totalProcessed}_Errors.log"), errSb.ToString(),
                    cts.Token);

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

    [GeneratedRegex(@"^\[.+\]$")]
    private static partial Regex PathRegex();

    [GeneratedRegex("^{.+}$")]
    private static partial Regex VariableRegex();

    static ForgeInstallResult GetCorruptedFileResult()
    {
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
    }

    void WhenCompleted(object? sender, DownloadFileCompletedEventArgs e)
    {
        if (sender is not SimpleDownloadFile file) return;
        if (!e.Success) this._failedFiles.Add(file);

        file.Completed -= this.WhenCompleted;

        this._totalDownloaded++;

        var progress = ProgressValue.Create(this._totalDownloaded, this._needToDownload);
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;

        this.InvokeStatusChangedEvent(
            $"{retryStr}下载 - {file.FileName} ( {this._totalDownloaded} / {this._needToDownload} )",
            progress);
    }
}