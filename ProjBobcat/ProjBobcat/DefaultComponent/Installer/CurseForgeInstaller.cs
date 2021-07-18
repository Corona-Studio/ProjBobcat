using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Interface;
using SharpCompress.Archives;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class CurseForgeInstaller : InstallerBase, ICurseForgeInstaller
    {
        private bool _isModAllDownloaded = true;

        private int _totalDownloaded, _needToDownload;
        public string ModPackPath { get; set; }
        public string GameId { get; set; }

        public void Install()
        {
            InstallTaskAsync().Wait();
        }

        public async Task InstallTaskAsync()
        {
            InvokeStatusChangedEvent("开始安装", 0);

            var manifest = await ReadManifestTask();
            var idPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(GameId));
            var downloadPath = Path.Combine(Path.GetFullPath(idPath), "mods");

            var di = new DirectoryInfo(downloadPath);

            if (!di.Exists)
                di.Create();

            _needToDownload = manifest.Files.Count;

            var urlBlock = new TransformManyBlock<IEnumerable<CurseForgeFileModel>, ValueTuple<long, long>>(urls =>
            {
                return urls.Select(file => (file.ProjectId, file.FileId));
            });

            var actionBlock = new ActionBlock<ValueTuple<long, long>>(async t =>
            {
                var downloadUrlRes = await CurseForgeAPIHelper.GetAddonDownloadUrl(t.Item1, t.Item2);
                var d = downloadUrlRes.Trim('"');
                var fn = Path.GetFileName(d);

                var downloadFile = new DownloadFile
                {
                    Completed = (_, args) =>
                    {
                        _totalDownloaded++;
                        _isModAllDownloaded = _isModAllDownloaded && (args.Success ?? false);

                        // if (!args.Success)
                        //     throw args.Error;

                        var progress = (double) _totalDownloaded / _needToDownload * 100;

                        InvokeStatusChangedEvent($"下载整合包中的 Mods - {fn} ({_totalDownloaded} / {_needToDownload})",
                            progress);
                    },
                    DownloadPath = di.FullName,
                    DownloadUri = d,
                    FileName = fn
                    // Host = "proxy.freecdn.workers.dev"
                };

                await DownloadHelper.DownloadData(downloadFile);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 32,
                MaxDegreeOfParallelism = 32
            });

            var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};
            urlBlock.LinkTo(actionBlock, linkOptions);
            urlBlock.Post(manifest.Files);
            urlBlock.Complete();

            await actionBlock.Completion;

            _totalDownloaded = 0;

            if (!_isModAllDownloaded)
                throw new NullReferenceException("未能下载全部的 Mods");

            using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));

            _totalDownloaded = 0;
            _needToDownload = archive.Entries.Count();

            foreach (var entry in archive.Entries)
            {
                if (!entry.Key.StartsWith(manifest.Overrides, StringComparison.OrdinalIgnoreCase)) continue;

                var subPath = entry.Key[(manifest.Overrides.Length + 1)..].Replace('/', '\\');
                if (string.IsNullOrEmpty(subPath)) continue;

                var path = Path.Combine(Path.GetFullPath(idPath), subPath);
                if (entry.IsDirectory)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    continue;
                }

                InvokeStatusChangedEvent($"解压缩安装文件：{subPath}", (double) _totalDownloaded / _needToDownload * 100);

                await using var fs = File.OpenWrite(path);
                entry.WriteTo(fs);

                _totalDownloaded++;
            }

            InvokeStatusChangedEvent("安装完成", 100);
        }

        public async Task<CurseForgeManifestModel> ReadManifestTask()
        {
            using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));
            var manifestEntry =
                archive.Entries.FirstOrDefault(x => x.Key.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

            if (manifestEntry == default)
                return default;

            await using var stream = manifestEntry.OpenEntryStream();
            using var sr = new StreamReader(stream, Encoding.UTF8);
            var content = await sr.ReadToEndAsync();

            var manifestModel = JsonConvert.DeserializeObject<CurseForgeManifestModel>(content);

            return manifestModel;
        }

        private static string CurseForgeModRequestUrl(long projectId, long fileId)
        {
            return $"https://addons-ecs.forgesvc.net/api/v2/addon/{projectId}/file/{fileId}/download-url";
        }
    }
}