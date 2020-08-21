using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            return ResolveResourceTaskAsync().GetAwaiter().GetResult();
        }

        public Task<IEnumerable<IGameResource>> ResolveResourceTaskAsync()
        {
            return Task.Run(() =>
            {
                LogGameResourceInfoResolveStatus("开始进行游戏资源(Library)检查");
                if (!(VersionInfo?.Natives?.Any() ?? false)) return default;
                if (!(VersionInfo?.Libraries?.Any() ?? false)) return default;

                var libDi = new DirectoryInfo(GamePathHelper.GetLibraryRootPath(BasePath));

                if (!libDi.Exists) libDi.Create();

                var lostLibrary = (from lib in VersionInfo.Libraries
                    where !File.Exists(GamePathHelper.GetLibraryPath(BasePath, lib.Path.Replace('/', '\\')))
                    select lib).ToList();

                lostLibrary.AddRange(from native in VersionInfo.Natives
                    where !File.Exists(GamePathHelper.GetLibraryPath(BasePath, native.FileInfo.Path.Replace('/', '\\')))
                    select native.FileInfo);

                var result = new List<IGameResource>();
                foreach (var lL in lostLibrary)
                {
                    string uri;
                    if (lL.Name.StartsWith("forge", StringComparison.Ordinal) ||
                        lL.Name.StartsWith("net.minecraftforge", StringComparison.Ordinal))
                        uri = $"{ForgeUriRoot}{lL.Path.Replace('\\', '/')}";
                    else
                        uri = $"{LibraryUriRoot}{lL.Path.Replace('\\', '/')}";

                    var index = lL.Path.LastIndexOf('/');
                    var path = GamePathHelper.GetLibraryPath(BasePath, lL.Path.Substring(0, index).Replace('/', '\\'));
                    result.Add(new LibraryDownloadInfo
                    {
                        FileName = lL.Path.Substring(index + 1),
                        Path = path,
                        Title = lL.Name.Split(':')[1],
                        Type = "Library/Native",
                        Uri = uri,
                        FileSize = lL.Size,
                        CheckSum = lL.Sha1
                    });
                }

                LogGameResourceInfoResolveStatus("检查Library完成");
                return (IEnumerable<IGameResource>) result;
            });
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