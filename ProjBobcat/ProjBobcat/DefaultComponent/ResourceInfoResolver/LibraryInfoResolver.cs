using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Interface;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public sealed class LibraryInfoResolver : ResolverBase
{
    public IReadOnlyList<DownloadUriInfo> LibraryUriRoots { get; init; } = [new("https://libraries.minecraft.net/", 1)];
    public IReadOnlyList<DownloadUriInfo> ForgeUriRoots { get; init; } = [new("https://files.minecraftforge.net/", 1)];
    public IReadOnlyList<DownloadUriInfo> FabricMavenUriRoots { get; init; } = [new("https://maven.fabricmc.net/", 1)];

    public IReadOnlyList<DownloadUriInfo> ForgeMavenUriRoots { get; init; } =
        [new("https://maven.minecraftforge.net/", 1)];

    public IReadOnlyList<DownloadUriInfo> ForgeMavenOldUriRoots { get; init; } =
        [new("https://files.minecraftforge.net/maven/", 1)];

    public IReadOnlyList<DownloadUriInfo> QuiltMavenUriRoots { get; init; } =
        [new("https://maven.quiltmc.org/repository/release/", 1)];

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
        string basePath,
        bool checkLocalFiles,
        ResolvedGameVersion resolvedGame)
    {
        if (!checkLocalFiles) yield break;

        this.OnResolve("开始进行游戏资源(Library)检查", ProgressValue.Start);
        if (resolvedGame.Natives.Count == 0 && resolvedGame.Libraries.Count == 0)
            yield break;

        var libDi = new DirectoryInfo(Path.Combine(basePath, GamePathHelper.GetLibraryRootPath()));

        if (!libDi.Exists) libDi.Create();

        var checkedLib = 0;
        var libCount = resolvedGame.Libraries.Count;

        foreach (var lib in resolvedGame.Libraries)
        {
            var libPath = GamePathHelper.GetLibraryPath(lib.Path!);
            var filePath = Path.Combine(basePath, libPath);

            var addedCheckedLib = Interlocked.Increment(ref checkedLib);
            var progress = ProgressValue.Create(addedCheckedLib, libCount);

            this.OnResolve(string.Empty, progress);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(lib.Sha1)) continue;

                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs).ConfigureAwait(false));

                if (computedHash.Equals(lib.Sha1, StringComparison.OrdinalIgnoreCase)) continue;
            }

            var downloadFile = this.GetDownloadFile(basePath, lib);

            if (downloadFile.Urls.Count == 0)
                continue;

            yield return downloadFile;
        }

        this.OnResolve("检索并验证 Library", ProgressValue.Start);

        checkedLib = 0;
        libCount = resolvedGame.Natives.Count;

        foreach (var native in resolvedGame.Natives)
        {
            var nativePath = GamePathHelper.GetLibraryPath(native.FileInfo.Path!);
            var filePath = Path.Combine(basePath, nativePath);

            if (File.Exists(filePath))
            {
                if (string.IsNullOrEmpty(native.FileInfo.Sha1)) continue;

                var addedCheckedLib = Interlocked.Increment(ref checkedLib);
                var progress = ProgressValue.Create(addedCheckedLib, libCount);

                this.OnResolve(string.Empty, progress);

                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs).ConfigureAwait(false));

                if (computedHash.Equals(native.FileInfo.Sha1, StringComparison.OrdinalIgnoreCase)) continue;
            }

            var downloadFile = this.GetDownloadFile(basePath, native.FileInfo);

            if (downloadFile.Urls.Count == 0)
                continue;

            yield return downloadFile;
        }

        this.OnResolve("检查Library完成", ProgressValue.Finished);
    }

    LibraryDownloadInfo GetDownloadFile(string basePath, FileInfo lL)
    {
        var libType = GetLibType(lL);
        var uris = libType switch
        {
            LibraryType.ForgeMaven => this.ForgeMavenUriRoots.Select(r =>
                r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            LibraryType.ForgeMavenOld =>
                this.ForgeMavenOldUriRoots.Select(r => r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            LibraryType.Forge => this.ForgeUriRoots.Select(r => r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            LibraryType.Fabric => this.FabricMavenUriRoots.Select(r =>
                r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            LibraryType.Quilt => this.QuiltMavenUriRoots.Select(r =>
                r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            LibraryType.ReplacementNative => [new DownloadUriInfo(lL.Url!, 1)],
            LibraryType.Other => this.LibraryUriRoots.Select(r => r with { DownloadUri = $"{r.DownloadUri}{lL.Path}" }),
            _ => []
        };

        var symbolIndex = lL.Path!.LastIndexOf('/');
        var fileName = lL.Path[(symbolIndex + 1)..];
        var path = Path.Combine(basePath,
            GamePathHelper.GetLibraryPath(lL.Path[..symbolIndex]));

        return new LibraryDownloadInfo
        {
            Path = path,
            Title = lL.Name?.Split(':')[1] ?? fileName,
            Type = ResourceType.LibraryOrNative,
            Urls = [.. uris],
            FileSize = lL.Size,
            CheckSum = lL.Sha1,
            FileName = fileName
        };
    }

    public static LibraryType GetLibType(FileInfo fi)
    {
        if (IsForgeLib(fi) &&
            !string.IsNullOrEmpty(fi.Url) &&
            fi.Url.StartsWith("https://maven.minecraftforge.net", StringComparison.OrdinalIgnoreCase))
            return LibraryType.ForgeMaven;

        if (IsForgeLib(fi) &&
            !string.IsNullOrEmpty(fi.Url) &&
            fi.Url.StartsWith("https://files.minecraftforge.net/maven/", StringComparison.OrdinalIgnoreCase))
            return LibraryType.ForgeMavenOld;

        if (IsForgeLib(fi)) return LibraryType.Forge;
        if (IsFabricLib(fi)) return LibraryType.Fabric;
        if (IsQuiltLib(fi) && !string.IsNullOrEmpty(fi.Url)) return LibraryType.Quilt;

        if (!string.IsNullOrEmpty(fi.Url) &&
            fi.Url.Contains("hmcl", StringComparison.OrdinalIgnoreCase))
            return LibraryType.ReplacementNative;

        return LibraryType.Other;
    }

    public static bool IsQuiltLib(FileInfo fi)
    {
        if (fi.Name?.Contains("quiltmc", StringComparison.OrdinalIgnoreCase) ?? false) return true;
        if (fi.Url?.Contains("quiltmc", StringComparison.OrdinalIgnoreCase) ?? false) return true;

        return false;
    }

    public static bool IsFabricLib(FileInfo fi)
    {
        if (fi.Name?.Contains("fabricmc", StringComparison.OrdinalIgnoreCase) ?? false) return true;
        if (fi.Url?.Contains("fabricmc", StringComparison.OrdinalIgnoreCase) ?? false) return true;

        return false;
    }

    public static bool IsForgeLib(FileInfo fi)
    {
        if (fi.Name?.StartsWith("forge", StringComparison.Ordinal) ?? false) return true;
        if (fi.Name?.StartsWith("net.minecraftforge", StringComparison.Ordinal) ?? false) return true;
        if (HttpHelper.RegexMatchUri(fi?.Url ?? string.Empty)
            .Contains("minecraftforge", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}