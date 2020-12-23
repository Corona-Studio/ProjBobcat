using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Interface;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ProjBobcat.DefaultComponent.Installer
{
    public class CurseForgeInstaller : InstallerBase, ICurseForgeInstaller
    {
        public string ModPackPath { get; set; }
        public string GameId { get; set; }

        private int _totalDownloaded, _needToDownload, _totalResolved, _needToResolve;
        private bool _isModAllDownloaded = true;

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
            
            if(!di.Exists)
                di.Create();

            _needToDownload = manifest.Files.Count;
            _needToResolve = manifest.Files.Count;

            var reqUrlBlock =
                new TransformManyBlock<IEnumerable<string>, string>(d => d, new ExecutionDataflowBlockOptions());

            var streamBlock = new TransformBlock<string, string>(async d =>
            {
                var downloadUrl = await HttpHelper.Get(d);

                InvokeStatusChangedEvent(
                    $"解析安装文件 - {Path.GetFileName(downloadUrl)} ({_totalResolved} / {_needToResolve})",
                    (double)_totalResolved / _needToResolve * 100);

                _totalResolved++;

                return downloadUrl;
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 8,
                MaxDegreeOfParallelism = 8
            });

            var actionBlock = new ActionBlock<string>(async d =>
            {
                var fileName = Path.GetFileName(d);
                var downloadFile = new DownloadFile
                {
                    Completed = (_, args) =>
                    {
                        _totalDownloaded++;
                        _isModAllDownloaded = _isModAllDownloaded && args.Success;

                        // if (!args.Success)
                        //     throw args.Error;

                        InvokeStatusChangedEvent($"下载整合包中的 Mods - {fileName} ({_totalDownloaded} / {_needToDownload})",
                            (double) _totalDownloaded / _needToDownload * 100);
                    },
                    DownloadPath = di.FullName,
                    DownloadUri = d,
                    FileName = fileName
                };

                await DownloadHelper.MultiPartDownloadTaskAsync(downloadFile);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 32,
                MaxDegreeOfParallelism = 32
            });

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            reqUrlBlock.LinkTo(streamBlock, linkOptions);
            streamBlock.LinkTo(actionBlock, linkOptions);

            var collections = manifest.Files.Select(file => CurseForgeModRequestUrl(file.ProjectId, file.FileId));
            reqUrlBlock.Post(collections);
            reqUrlBlock.Complete();

            await actionBlock.Completion;

            _totalDownloaded = 0;

            if (_isModAllDownloaded)
                throw new NullReferenceException("未能下载全部的 Mods");

            using var archive = ArchiveFactory.Open(Path.GetFullPath(ModPackPath));

            _totalDownloaded = 0;
            _needToDownload = archive.Entries.Count();
            
            foreach (var entry in archive.Entries)
            {
                InvokeStatusChangedEvent("解压缩安装文件", (double) _totalDownloaded / _needToDownload * 100);

                if (!entry.Key.StartsWith(manifest.Overrides, StringComparison.OrdinalIgnoreCase)) continue;

                var subPath = entry.Key[manifest.Overrides.Length..];
                var path = Path.Combine(Path.GetFullPath(idPath), subPath);

                var entryDi = new DirectoryInfo(path);
                
                if(!entryDi.Exists)
                    entryDi.Create();

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

        private static string CurseForgeModRequestUrl(long projectId, long fileId) =>
            $"https://addons-ecs.forgesvc.net/api/v2/addon/{projectId}/file/{fileId}/download-url";
    }
}