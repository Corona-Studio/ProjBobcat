using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Archives;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller;

public partial class HighVersionForgeInstaller : InstallerBase, IForgeInstaller
{
    readonly ConcurrentBag<DownloadFile> _failedFiles = [];
    int _totalDownloaded, _needToDownload, _totalProcessed, _needToProcess;

    public required string JavaExecutablePath { get; init; }
    public required string MineCraftVersionId { get; init; }
    public required string MineCraftVersion { get; init; }

    public FileInfo? CustomMojangClientMappings { get; init; }
    public override required string RootPath { get; init; }
    public required string DownloadUrlRoot { get; init; }
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

        using var archive = ArchiveFactory.Open(Path.GetFullPath(this.ForgeExecutablePath));

        #region 解析 Version.json

        this.InvokeStatusChangedEvent("解析 Version.json", 0.1);

        var versionJsonEntry =
            archive.Entries.FirstOrDefault(e =>
                e.Key?.Equals("version.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (versionJsonEntry == default)
            return this.GetCorruptedFileResult();

        await using var stream = versionJsonEntry.OpenEntryStream();
        var versionJsonModel =
            await JsonSerializer.DeserializeAsync(stream, RawVersionModelContext.Default.RawVersionModel);

        if (versionJsonModel == default)
            return this.GetCorruptedFileResult();

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

        this.InvokeStatusChangedEvent("解析 Install_profile.json", 0.2);

        var installProfileEntry =
            archive.Entries.FirstOrDefault(e =>
                e.Key?.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (installProfileEntry == default)
            return this.GetCorruptedFileResult();

        await using var ipStream = installProfileEntry.OpenEntryStream();
        var ipModel =
            await JsonSerializer.DeserializeAsync(ipStream, ForgeInstallProfileContext.Default.ForgeInstallProfile);

        #endregion

        ArgumentNullException.ThrowIfNull(ipModel, "Forge install_profile is null, please check downloaded JAR!");

        #region 解析 Lzma

        this.InvokeStatusChangedEvent("解析 Lzma", 0.4);

        var serverLzma = archive.Entries.FirstOrDefault(e =>
            e.Key?.Equals("data/server.lzma", StringComparison.OrdinalIgnoreCase) ?? false);
        var clientLzma = archive.Entries.FirstOrDefault(e =>
            e.Key?.Equals("data/client.lzma", StringComparison.OrdinalIgnoreCase) ?? false);

        if (serverLzma != default)
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

            serverLzma.WriteTo(sFs);
        }

        if (clientLzma != default)
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
            clientLzma.WriteTo(cFs);
        }

        #endregion

        #region 解压 Forge Jar

        this.InvokeStatusChangedEvent("解压 Forge Jar", 0.5);

        var forgeJar = archive.Entries.FirstOrDefault(e =>
            e.Key?.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}.jar",
                StringComparison.OrdinalIgnoreCase) ?? false);
        var forgeUniversalJar = archive.Entries.FirstOrDefault(e =>
            e.Key?.Equals($"maven/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-universal.jar",
                StringComparison.OrdinalIgnoreCase) ?? false);

        if (forgeJar != default)
        {
            if (forgeUniversalJar != default)
            {
                var forgeUniversalSubPath = forgeUniversalJar.Key![(forgeUniversalJar.Key!.IndexOf('/') + 1)..];
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
                forgeUniversalJar.WriteTo(forgeUniversalFs);
            }

            var forgeSubPath = forgeJar.Key![(forgeJar.Key!.IndexOf('/') + 1)..];
            var forgeLibPath =
                Path.Combine(this.RootPath, GamePathHelper.GetLibraryPath(forgeSubPath));

            var forgeLibDir = Path.GetDirectoryName(forgeLibPath)!;
            if (!Directory.Exists(forgeLibDir))
                Directory.CreateDirectory(forgeLibDir);

            await using var forgeFs = File.OpenWrite(forgeLibPath);

            var fLDi = new DirectoryInfo(Path.GetDirectoryName(forgeLibPath)!);

            if (!fLDi.Exists)
                fLDi.Create();

            forgeJar.WriteTo(forgeFs);
        }

        #endregion

        #region 预下载 Mojang Mappings

        if (ipModel.Data.TryGetValue("MOJMAPS", out var mapsVal) &&
            !string.IsNullOrEmpty(mapsVal.Client) && this.CustomMojangClientMappings != null &&
            !string.IsNullOrEmpty(this.CustomMojangClientMappings.Url))
        {
            var clientMavenStr = mapsVal.Client.TrimStart('[').TrimEnd(']');
            var resolvedMappingMaven = clientMavenStr.ResolveMavenString()!;
            var mappingPath = Path.GetDirectoryName(resolvedMappingMaven.Path);
            var mappingFileName = Path.GetFileName(resolvedMappingMaven.Path);
            var mappingDf = new DownloadFile
            {
                CheckSum = this.CustomMojangClientMappings.Sha1,
                DownloadPath = mappingPath!,
                DownloadUri = this.CustomMojangClientMappings.Url,
                FileName = mappingFileName
            };

            mappingDf.Changed += (_, args) =>
            {
                this.InvokeStatusChangedEvent(
                    $"下载 - {mappingFileName} ( {args.ProgressPercentage} / 100 )",
                    args.ProgressPercentage);
            };

            if (!Directory.Exists(mappingPath))
                Directory.CreateDirectory(mappingPath!);

            await DownloadHelper.MultiPartDownloadTaskAsync(mappingDf, new DownloadSettings
            {
                CheckFile = true,
                DownloadParts = 4,
                HashType = HashType.SHA1,
                RetryCount = 3,
                Timeout = TimeSpan.FromSeconds(5)
            });
        }

        #endregion

        #region 解析 Processor

        this.InvokeStatusChangedEvent("解析 Processor", 1);

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
        var libDownloadInfo = new List<DownloadFile>();

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

            var df = new DownloadFile
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
            RetryCount = 3,
            Timeout = TimeSpan.FromSeconds(20)
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

            using var libArchive = ArchiveFactory.Open(Path.GetFullPath(libPath));
            var libEntry =
                libArchive.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase) ?? false);

            if (libEntry == null)
                return this.GetCorruptedFileResult();

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
                var progress = (double)this._totalProcessed / this._needToProcess;
                var dataStr = data.CropStr(40);

                this.InvokeStatusChangedEvent($"{dataStr} <安装信息> ( {this._totalProcessed} / {this._needToProcess} )",
                    progress);
            };

            p.ErrorDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;

                errSb.AppendLine(args.Data);

                var data = args.Data ?? string.Empty;
                var progress = (double)this._totalProcessed / this._needToProcess;
                var dataStr = data.CropStr(40);

                this.InvokeStatusChangedEvent($"{dataStr} <错误> ( {this._totalProcessed} / {this._needToProcess} )",
                    progress);
            };

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            this._totalProcessed++;
            await p.WaitForExitAsync();

            var installLogPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));

            if (logSb.Length != 0)
                await File.WriteAllTextAsync(
                    Path.Combine(installLogPath, $"PROCESSOR #{this._totalProcessed}_Logs.log"),
                    logSb.ToString());
            if (errSb.Length != 0)
                await File.WriteAllTextAsync(
                    Path.Combine(installLogPath, $"PROCESSOR #{this._totalProcessed}_Errors.log"), errSb.ToString());

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

    ForgeInstallResult GetCorruptedFileResult()
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
        if (sender is not DownloadFile file) return;

        this._totalDownloaded++;

        var progress = (double)this._totalDownloaded / this._needToDownload;
        var retryStr = file.RetryCount > 0 ? $"[重试 - {file.RetryCount}] " : string.Empty;

        this.InvokeStatusChangedEvent(
            $"{retryStr}下载 - {file.FileName} ( {this._totalDownloaded} / {this._needToDownload} )",
            progress);

        if (!e.Success) this._failedFiles.Add(file);
    }
}