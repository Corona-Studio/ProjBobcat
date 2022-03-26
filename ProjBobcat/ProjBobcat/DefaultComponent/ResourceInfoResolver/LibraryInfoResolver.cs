using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public class LibraryInfoResolver : ResolverBase
{
    public string LibraryUriRoot { get; init; } = "https://libraries.minecraft.net/";
    public string ForgeUriRoot { get; init; } = "https://files.minecraftforge.net/";
    public string FabricMavenUriRoot { get; init; } = "https://maven.fabricmc.net";
    public string ForgeMavenUriRoot { get; init; } = "https://maven.minecraftforge.net/";
    public string ForgeMavenOldUriRoot { get; init; } = "https://files.minecraftforge.net/maven/";

    public override async Task<IEnumerable<IGameResource>> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) return Enumerable.Empty<IGameResource>();

        OnResolve("开始进行游戏资源(Library)检查");
        if (!(VersionInfo?.Natives?.Any() ?? false) &&
            !(VersionInfo?.Libraries?.Any() ?? false))
            return Enumerable.Empty<IGameResource>();

        var libDi = new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetLibraryRootPath()));

        if (!libDi.Exists) libDi.Create();

        var checkedLib = 0;
        var libCount = VersionInfo.Libraries.Count;
        var checkedResult = new ConcurrentBag<FileInfo>();
        var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

        var libFilesBlock =
            new TransformManyBlock<IEnumerable<FileInfo>, FileInfo>(chunk => chunk,
                new ExecutionDataflowBlockOptions());

        var libResolveActionBlock = new ActionBlock<FileInfo>(async lib =>
        {
            var libPath = GamePathHelper.GetLibraryPath(lib.Path.Replace('/', '\\'));
            var filePath = Path.Combine(BasePath, libPath);

            Interlocked.Increment(ref checkedLib);
            var progress = (double) checkedLib / libCount * 100;

            OnResolve(string.Empty, progress);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(lib.Sha1)) return;

                var bytes = await File.ReadAllBytesAsync(filePath);
                var computedHash = CryptoHelper.ToString(SHA1.HashData(bytes.AsSpan()));

                if (computedHash.Equals(lib.Sha1, StringComparison.OrdinalIgnoreCase)) return;
            }

            checkedResult.Add(lib);
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            BoundedCapacity = MaxDegreeOfParallelism
        });

        OnResolve("检索并验证 Library");

        libFilesBlock.LinkTo(libResolveActionBlock, linkOptions);
        libFilesBlock.Post(VersionInfo.Libraries);
        libFilesBlock.Complete();

        await libResolveActionBlock.Completion;
        libResolveActionBlock.Complete();

        checkedLib = 0;
        libCount = VersionInfo.Natives.Count;

        var nativeFilesBlock =
            new TransformManyBlock<IEnumerable<NativeFileInfo>, NativeFileInfo>(chunk => chunk,
                new ExecutionDataflowBlockOptions());

        var nativeResolveActionBlock = new ActionBlock<NativeFileInfo>(async native =>
        {
            var nativePath = GamePathHelper.GetLibraryPath(native.FileInfo.Path.Replace('/', '\\'));
            var filePath = Path.Combine(BasePath, nativePath);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(native.FileInfo.Sha1)) return;

                Interlocked.Increment(ref checkedLib);
                var progress = (double) checkedLib / libCount * 100;
                OnResolve(string.Empty, progress);

                var computedHash = CryptoHelper.ToString(SHA1.HashData(await File.ReadAllBytesAsync(filePath)));

                if (computedHash.Equals(native.FileInfo.Sha1, StringComparison.OrdinalIgnoreCase)) return;
            }

            checkedResult.Add(native.FileInfo);
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            BoundedCapacity = MaxDegreeOfParallelism
        });

        OnResolve("检索并验证 Native");

        nativeFilesBlock.LinkTo(nativeResolveActionBlock, linkOptions);
        nativeFilesBlock.Post(VersionInfo.Natives);
        nativeFilesBlock.Complete();

        await nativeResolveActionBlock.Completion;
        nativeResolveActionBlock.Complete();

        var result = new List<IGameResource>();
        foreach (var lL in checkedResult)
        {
            var libType = GetLibType(lL);
            var uri = libType switch
            {
                LibraryType.Forge when lL.Url?.StartsWith("https://maven.minecraftforge.net",
                    StringComparison.OrdinalIgnoreCase) ?? false => $"{ForgeMavenUriRoot}{lL.Path.Replace('\\', '/')}",
                LibraryType.Forge when lL.Url?.StartsWith("https://files.minecraftforge.net/maven/",
                                           StringComparison.OrdinalIgnoreCase) ??
                                       false => $"{ForgeMavenOldUriRoot}{lL.Path.Replace('\\', '/')}",
                LibraryType.Forge => $"{ForgeUriRoot}{lL.Path.Replace('\\', '/')}",
                LibraryType.Fabric => $"{FabricMavenUriRoot}{lL.Path.Replace('\\', '/')}",
                LibraryType.Other => $"{LibraryUriRoot}{lL.Path.Replace('\\', '/')}",
                _ => string.Empty
            };

            var symbolIndex = lL.Path.LastIndexOf('/');
            var fileName = lL.Path[(symbolIndex + 1)..];
            var path = Path.Combine(BasePath,
                GamePathHelper.GetLibraryPath(lL.Path[..symbolIndex].Replace('/', '\\')));

            result.Add(
                new LibraryDownloadInfo
                {
                    Path = path,
                    Title = lL.Name.Split(':')[1],
                    Type = ResourceType.LibraryOrNative,
                    Uri = uri,
                    FileSize = lL.Size,
                    CheckSum = lL.Sha1,
                    FileName = fileName
                });
        }

        OnResolve("检查Library完成", 100);

        return result;
    }

    static LibraryType GetLibType(FileInfo fi)
    {
        if (IsForgeLib(fi)) return LibraryType.Forge;
        if (IsFabricLib(fi)) return LibraryType.Fabric;

        return LibraryType.Other;
    }

    static bool IsFabricLib(FileInfo fi)
    {
        if (fi.Name.Contains("fabricmc", StringComparison.OrdinalIgnoreCase)) return true;
        if (fi.Url.Contains("fabricmc", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    static bool IsForgeLib(FileInfo fi)
    {
        if (fi.Name.StartsWith("forge", StringComparison.Ordinal)) return true;
        if (fi.Name.StartsWith("net.minecraftforge", StringComparison.Ordinal)) return true;
        if (HttpHelper.RegexMatchUri(fi?.Url ?? string.Empty)
            .Contains("minecraftforge", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}