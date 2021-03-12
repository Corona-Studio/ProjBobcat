using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class LibraryInfoResolver : IResourceInfoResolver
    {
        public string LibraryUriRoot { get; set; } = "https://libraries.minecraft.net/";
        public string ForgeUriRoot { get; set; } = "https://files.minecraftforge.net/";
        public string BasePath { get; set; }
        public VersionInfo VersionInfo { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;

        public IEnumerable<IGameResource> ResolveResource()
        {
            var itr = ResolveResourceAsync().GetAsyncEnumerator();
            while (itr.MoveNextAsync().Result) yield return itr.Current;
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
        {
            LogGameResourceInfoResolveStatus("开始进行游戏资源(Library)检查");
            if (!(VersionInfo?.Natives?.Any() ?? false)) yield break;
            if (!(VersionInfo?.Libraries?.Any() ?? false)) yield break;

            var libDi = new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetLibraryRootPath()));

            if (!libDi.Exists) libDi.Create();

            var lostLibrary = (from lib in VersionInfo.Libraries
                where !File.Exists(Path.Combine(BasePath,
                    GamePathHelper.GetLibraryPath(lib.Path.Replace('/', '\\'))))
                select lib).ToList();

            lostLibrary.AddRange(from native in VersionInfo.Natives
                where !File.Exists(Path.Combine(BasePath,
                    GamePathHelper.GetLibraryPath(native.FileInfo.Path.Replace('/', '\\'))))
                select native.FileInfo);

            foreach (var lL in lostLibrary)
            {
                string uri;
                if (lL.Name.StartsWith("forge", StringComparison.Ordinal) ||
                    lL.Name.StartsWith("net.minecraftforge", StringComparison.Ordinal))
                    uri = $"{ForgeUriRoot}{lL.Path.Replace('\\', '/')}";
                else
                    uri = $"{LibraryUriRoot}{lL.Path.Replace('\\', '/')}";

                var symbolIndex = lL.Path.LastIndexOf('/');
                var fileName = lL.Path[(symbolIndex + 1)..];
                var path = Path.Combine(BasePath,
                    GamePathHelper.GetLibraryPath(lL.Path[..symbolIndex].Replace('/', '\\')));

                yield return new LibraryDownloadInfo
                {
                    Path = path,
                    Title = lL.Name.Split(':')[1],
                    Type = "Library/Native",
                    Uri = uri,
                    FileSize = lL.Size,
                    CheckSum = lL.Sha1,
                    FileName = fileName
                };
            }

            LogGameResourceInfoResolveStatus("检查Library完成");
        }

        private void LogGameResourceInfoResolveStatus(string currentStatus, LogType logType = LogType.Normal)
        {
            GameResourceInfoResolveEvent?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                CurrentProgress = currentStatus,
                LogType = logType
            });
        }
    }
}