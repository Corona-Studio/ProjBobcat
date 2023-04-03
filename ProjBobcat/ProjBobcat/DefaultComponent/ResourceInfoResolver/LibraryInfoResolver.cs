using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class LibraryInfoResolver : ResolverBase
{
    public string LibraryUriRoot { get; init; } = "https://libraries.minecraft.net/";
    public string ForgeUriRoot { get; init; } = "https://files.minecraftforge.net/";
    public string FabricMavenUriRoot { get; init; } = "https://maven.fabricmc.net";
    public string ForgeMavenUriRoot { get; init; } = "https://maven.minecraftforge.net/";
    public string ForgeMavenOldUriRoot { get; init; } = "https://files.minecraftforge.net/maven/";
    public string QuiltMavenUriRoot { get; init; } = "https://maven.quiltmc.org/repository/release/";

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (!CheckLocalFiles) yield break;

        OnResolve("开始进行游戏资源(Library)检查");
        if (!(VersionInfo?.Natives?.Any() ?? false) &&
            !(VersionInfo?.Libraries?.Any() ?? false))
            yield break;

        var libDi = new DirectoryInfo(Path.Combine(BasePath, GamePathHelper.GetLibraryRootPath()));

        if (!libDi.Exists) libDi.Create();

        var checkedLib = 0;
        var libCount = VersionInfo.Libraries.Count;

        foreach (var lib in VersionInfo.Libraries)
        {
            var libPath = GamePathHelper.GetLibraryPath(lib.Path);
            var filePath = Path.Combine(BasePath, libPath);

            Interlocked.Increment(ref checkedLib);
            var progress = (double)checkedLib / libCount * 100;

            OnResolve(string.Empty, progress);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(lib.Sha1)) continue;

#if NET7_0_OR_GREATER
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = (await SHA1.HashDataAsync(fs)).BytesToString();
#else
                var bytes = await File.ReadAllBytesAsync(filePath);
                var computedHash = SHA1.HashData(bytes.AsSpan()).BytesToString();
#endif

                if (computedHash.Equals(lib.Sha1, StringComparison.OrdinalIgnoreCase)) continue;
            }

            yield return GetDownloadFile(lib);
        }

        OnResolve("检索并验证 Library");

        checkedLib = 0;
        libCount = VersionInfo.Natives.Count;

        foreach (var native in VersionInfo.Natives)
        {
            var nativePath = GamePathHelper.GetLibraryPath(native.FileInfo.Path);
            var filePath = Path.Combine(BasePath, nativePath);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(native.FileInfo.Sha1)) continue;

                Interlocked.Increment(ref checkedLib);
                var progress = (double)checkedLib / libCount * 100;
                OnResolve(string.Empty, progress);

#if NET7_0_OR_GREATER
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = (await SHA1.HashDataAsync(fs)).BytesToString();
#else
                var bytes = await File.ReadAllBytesAsync(filePath);
                var computedHash = SHA1.HashData(bytes.AsSpan()).BytesToString();
#endif

                if (computedHash.Equals(native.FileInfo.Sha1, StringComparison.OrdinalIgnoreCase)) continue;
            }

            yield return GetDownloadFile(native.FileInfo);
        }

        OnResolve("检查Library完成", 100);
    }

    LibraryDownloadInfo GetDownloadFile(FileInfo lL)
    {
        var libType = GetLibType(lL);
        var uri = libType switch
        {
            LibraryType.Forge when lL.Url?.StartsWith("https://maven.minecraftforge.net",
                StringComparison.OrdinalIgnoreCase) ?? false => $"{ForgeMavenUriRoot}{lL.Path}",
            LibraryType.Forge when lL.Url?.StartsWith("https://files.minecraftforge.net/maven/",
                                       StringComparison.OrdinalIgnoreCase) ??
                                   false => $"{ForgeMavenOldUriRoot}{lL.Path}",
            LibraryType.Forge => $"{ForgeUriRoot}{lL.Path}",
            LibraryType.Fabric => $"{FabricMavenUriRoot}{lL.Path}",
            LibraryType.Quilt when !string.IsNullOrEmpty(lL.Url) =>
                $"{QuiltMavenUriRoot}{lL.Path}",
            LibraryType.Other => $"{LibraryUriRoot}{lL.Path}",
            _ => string.Empty
        };

        var symbolIndex = lL.Path.LastIndexOf('/');
        var fileName = lL.Path[(symbolIndex + 1)..];
        var path = Path.Combine(BasePath,
            GamePathHelper.GetLibraryPath(lL.Path[..symbolIndex]));

        return new LibraryDownloadInfo
        {
            Path = path,
            Title = lL.Name.Split(':')[1],
            Type = ResourceType.LibraryOrNative,
            Url = uri,
            FileSize = lL.Size,
            CheckSum = lL.Sha1,
            FileName = fileName
        };
    }

    static LibraryType GetLibType(FileInfo fi)
    {
        if (IsForgeLib(fi)) return LibraryType.Forge;
        if (IsFabricLib(fi)) return LibraryType.Fabric;
        if (IsQuiltLib(fi)) return LibraryType.Quilt;

        return LibraryType.Other;
    }

    static bool IsQuiltLib(FileInfo fi)
    {
        if (fi.Name.Contains("quiltmc", StringComparison.OrdinalIgnoreCase)) return true;
        if (fi.Url.Contains("quiltmc", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
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