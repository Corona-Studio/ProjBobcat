using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class LibraryInfoResolver : IResourceInfoResolver
    {
        public string LibraryUriRoot { get; init; } = "https://libraries.minecraft.net/";
        public string ForgeUriRoot { get; init; } = "https://files.minecraftforge.net/";
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

#pragma warning disable CA5350 // 不要使用弱加密算法
            using var hA = SHA1.Create();
#pragma warning restore CA5350 // 不要使用弱加密算法

            var libraries = new List<FileInfo>();
            foreach (var lib in VersionInfo.Libraries)
            {
                var libPath = GamePathHelper.GetLibraryPath(lib.Path.Replace('/', '\\'));
                var filePath = Path.Combine(BasePath, libPath);

                LogGameResourceInfoResolveStatus($"检索并验证 Library：{libPath}");

                if (File.Exists(filePath))
                {
                    if(string.IsNullOrEmpty(lib.Sha1)) continue;

                    try
                    {
                        var computedHash = await CryptoHelper.ComputeFileHashAsync(filePath, hA);
                        if (computedHash.Equals(lib.Sha1, StringComparison.OrdinalIgnoreCase)) continue;

                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                libraries.Add(lib);
            }

            var natives = new List<FileInfo>();
            foreach (var native in VersionInfo.Natives)
            {
                var nativePath = GamePathHelper.GetLibraryPath(native.FileInfo.Path.Replace('/', '\\'));
                var filePath = Path.Combine(BasePath, nativePath);

                LogGameResourceInfoResolveStatus($"检索并验证 Native：{nativePath}");

                if (File.Exists(filePath))
                {
                    if (string.IsNullOrEmpty(native.FileInfo.Sha1)) continue;

                    try
                    {
                        var computedHash = await CryptoHelper.ComputeFileHashAsync(filePath, hA);
                        if (computedHash.Equals(native.FileInfo.Sha1, StringComparison.OrdinalIgnoreCase)) continue;

                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                natives.Add(native.FileInfo);
            }

            foreach (var lL in libraries.Union(natives))
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