using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public class HighVersionForgeInstaller : InstallerBase, IForgeInstaller
    {
        private int _totalDownloaded, _needToDownload, _totalProcessed, _needToProcess;
        public string JavaExecutablePath { get; init; }

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
            using var sr = new StreamReader(stream, Encoding.UTF8);
            var versionJsonContent = await sr.ReadToEndAsync();
            var versionJsonModel = JsonConvert.DeserializeObject<RawVersionModel>(versionJsonContent);

            var forgeVersion = versionJsonModel.Id.Replace("-forge-", "-");
            var id = string.IsNullOrEmpty(CustomId) ? versionJsonModel.Id : CustomId;

            versionJsonModel.Id = id;

            var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);

            await using var fs = File.OpenWrite(jsonPath);
            versionJsonEntry.WriteTo(fs);

            #endregion

            #region 解析 Install_profile.json

            InvokeStatusChangedEvent("解析 Install_profile.json", 0.2);

            var installProfileEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.Key.Equals("install_profile.json", StringComparison.OrdinalIgnoreCase));

            await using var ipStream = installProfileEntry.OpenEntryStream();
            using var ipSr = new StreamReader(ipStream, Encoding.UTF8);
            var ipContent = await ipSr.ReadToEndAsync();
            var ipModel = JsonConvert.DeserializeObject<ForgeInstallProfile>(ipContent);

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
                    GamePathHelper.GetLibraryPath(serverBinMaven.Path.Replace('/', '\\')));

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
                    GamePathHelper.GetLibraryPath(clientBinMaven.Path.Replace('/', '\\')));

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


            var forgeSubPath = forgeJar.Key[(forgeJar.Key.IndexOf('/') + 1)..];
            var forgeLibPath = Path.Combine(RootPath, GamePathHelper.GetLibraryPath(forgeSubPath.Replace('/', '\\')));

            var forgeUniversalSubPath = forgeUniversalJar.Key[(forgeUniversalJar.Key.IndexOf('/') + 1)..];
            var forgeUniversalLibPath = Path.Combine(RootPath,
                GamePathHelper.GetLibraryPath(forgeUniversalSubPath.Replace('/', '\\')));

            await using var forgeUniversalFs = File.OpenWrite(forgeUniversalLibPath);
            await using var forgeFs = File.OpenWrite(forgeLibPath);

            var fLDi = new DirectoryInfo(Path.GetDirectoryName(forgeLibPath));

            if (!fLDi.Exists)
                fLDi.Create();

            forgeJar.WriteTo(forgeFs);
            forgeUniversalJar.WriteTo(forgeUniversalFs);

            #endregion

            #region 解析 Processor

            InvokeStatusChangedEvent("解析 Processor", 1);

            var pathRegex = new Regex("^\\[.+\\]$");
            var variableRegex = new Regex("^{.+}$");

            string ResolvePathRegex(string val)
            {
                if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(pathRegex.Match(val).Value)) return val;

                var name = val[1..^1];
                var maven = name.ResolveMavenString();
                var path = Path.Combine(RootPath,
                    GamePathHelper.GetLibraryPath(maven.Path.Replace('/', '\\')).Replace('/', '\\'));

                return path;
            }

            var variables = new Dictionary<string, ForgeInstallProfileData>
            {
                {
                    "MINECRAFT_JAR",
                    new ForgeInstallProfileData
                    {
                        Client = GamePathHelper.GetVersionJar(RootPath, ipModel.MineCraft)
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
                if (string.IsNullOrEmpty(val) || string.IsNullOrEmpty(variableRegex.Match(val).Value)) return val;

                var key = val[1..^1];
                return variables[key].Client;
            }

            var procList = new List<ForgeInstallProcessorModel>();
            foreach (var proc in ipModel.Processors)
            {
                var outputs = new Dictionary<string, string>();

                if (proc.Outputs?.Any() ?? false)
                    foreach (var (k, v) in proc.Outputs)
                        outputs.TryAdd(ResolveVariableRegex(k), ResolveVariableRegex(v));

                var model = new ForgeInstallProcessorModel
                {
                    Processor = proc,
                    Arguments = proc.Arguments.Select(ResolvePathRegex).Select(ResolveVariableRegex).ToList(),
                    Outputs = outputs
                };

                procList.Add(model);
            }

            #endregion

            #region 补全 Libraries

            var libs = ipModel.Libraries;
            libs.AddRange(versionJsonModel.Libraries);

            var resolvedLibs = VersionLocator.GetNatives(libs).Item2;
            var libDownloadInfo = new List<DownloadFile>();

            foreach (var lib in resolvedLibs)
            {
                if (lib.Name.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase))
                    continue;

                var symbolIndex = lib.Path.LastIndexOf('/');
                var fileName = lib.Path[(symbolIndex + 1)..];
                var path = Path.Combine(RootPath,
                    GamePathHelper.GetLibraryPath(lib.Path[..symbolIndex].Replace('/', '\\')));

                if (!string.IsNullOrEmpty(DownloadUrlRoot))
                {
                    var urlRoot = HttpHelper.RegexMatchUri(lib.Url);
                    var url = lib.Url.Replace($"{urlRoot}/", string.Empty);
                    if (!url.StartsWith("maven", StringComparison.OrdinalIgnoreCase))
                        url = "maven/" + url;

                    lib.Url = $"{DownloadUrlRoot}{url}";
                }

                var libDi = new DirectoryInfo(path);

                if (!libDi.Exists)
                    libDi.Create();

                var df = new DownloadFile
                {
                    Completed = (_, args) =>
                    {
                        _totalDownloaded++;
                        var progress = (double) _totalDownloaded / _needToDownload;
                        InvokeStatusChangedEvent(
                            $"下载 Forge Library - {args.File.FileName} ( {_totalDownloaded} / {_needToDownload} )",
                            progress);
                    },
                    CheckSum = lib.Sha1,
                    DownloadPath = path,
                    FileName = fileName,
                    DownloadUri = lib.Url,
                    FileSize = lib.Size
                };

                libDownloadInfo.Add(df);
            }

            _needToDownload = libDownloadInfo.Count;
            await DownloadHelper.AdvancedDownloadListFile(libDownloadInfo);

            #endregion

            #region 启动 Process

            _needToProcess = procList.Count;
            foreach (var processor in procList)
            {
                var maven = processor.Processor.Jar.ResolveMavenString();
                var libPath = Path.Combine(RootPath, GamePathHelper.GetLibraryPath(maven.Path.Replace('/', '\\')));

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

                var totalLibs = processor.Processor.ClassPath;
                totalLibs.Add(processor.Processor.Jar);

                var cp = totalLibs.Select(MavenHelper.ResolveMavenString)
                    .Select(m => Path.Combine(RootPath, GamePathHelper.GetLibraryPath(m.Path).Replace('/', '\\')));
                var cpStr = string.Join(';', cp);
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

                var p = Process.Start(pi);

                p.OutputDataReceived += (_, args) =>
                {
                    if (string.IsNullOrEmpty(args.Data)) return;

                    var progress = (double) _totalProcessed / _needToProcess;
                    InvokeStatusChangedEvent($"{args.Data} <安装信息> ( {_totalProcessed} / {_needToProcess} )", progress);
                };

                p.ErrorDataReceived += (_, args) =>
                {
                    if (string.IsNullOrEmpty(args.Data)) return;

                    var progress = (double) _totalProcessed / _needToProcess;
                    InvokeStatusChangedEvent($"{args.Data} <错误> ( {_totalProcessed} / {_needToProcess} )", progress);
                };

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                _totalProcessed++;
                await p.WaitForExitAsync();
            }

            #endregion

            return new ForgeInstallResult
            {
                Succeeded = true
            };
        }
    }
}