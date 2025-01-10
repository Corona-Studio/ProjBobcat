using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonRepairSharp.Class;
using ProjBobcat.Class.Helper.TOMLParser;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.GameResource.ResolvedInfo;
using SharpCompress.Archives;

namespace ProjBobcat.Class.Helper;

public static class GameResourcesResolveHelper
{
    static async Task<GameModResolvedInfo?> GetTomlModInfo(
        IArchiveEntry entry,
        string file,
        bool isEnabled,
        bool isNeoForge)
    {
        await using var stream = entry.OpenEntryStream();
        using var sR = new StreamReader(stream);
        using var parser = new TOMLParser.TOMLParser(sR);
        var pResult = parser.TryParse(out var table, out _);

        if (!pResult) return null;
        if (!table.HasKey("mods")) return null;

        var innerTable = table["mods"];

        if (innerTable is not TomlArray arr) return null;
        if (arr.ChildrenCount == 0) return null;

        var infoTable = arr.Children.First();

        var title = infoTable.HasKey("modId")
            ? infoTable["modId"]?.AsString ?? "-"
            : Path.GetFileName(file);
        var author = infoTable.HasKey("authors")
            ? infoTable["authors"]?.AsString
            : null;
        var version = infoTable.HasKey("version")
            ? infoTable["version"]?.AsString
            : null;

        var depsKey = $"dependencies.{title}";
        var dependencies = table.HasKey(depsKey)
            ? table[depsKey] as TomlArray
            : null;

        var modList = new List<string>();

        if (dependencies != null)
        {
            foreach (var dep in dependencies.Children)
            {
                if (!dep.HasKey("modId")) continue;
                if (!dep.HasKey("versionRange"))
                {
                    modList.Add(dep["modId"]?.AsString ?? string.Empty);
                    continue;
                }

                modList.Add(
                    $"{dep["modId"]?.AsString ?? string.Empty} ({dep["versionRange"]?.AsString ?? string.Empty})");
            }
        }

        return new GameModResolvedInfo(
            author?.Value,
            file,
            modList.Select(m => m.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).ToImmutableList(),
            title,
            version?.Value,
            isNeoForge ? "NeoForge" : "Forge",
            isEnabled);
    }

    static async Task<GameModResolvedInfo?> GetNewModInfo(
        IArchiveEntry entry,
        string file,
        bool isEnabled,
        CancellationToken ct)
    {
        try
        {
            GameModInfoModel[]? model = null;

            await using var stream = entry.OpenEntryStream();
            using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            var json = await sr.ReadToEndAsync(ct);
            var fixedJson = JsonRepairCore.JsonRepair(json);

            await using var fixedStream = new MemoryStream(Encoding.UTF8.GetBytes(fixedJson));

            var element = (await JsonDocument.ParseAsync(fixedStream, cancellationToken: ct)).RootElement;

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty("modList", out var innerModList))
                    {
                        element = innerModList;
                        goto case JsonValueKind.Array;
                    }

                    var val = element.Deserialize(GameModInfoModelContext.Default.GameModInfoModel);

                    if (val != null) model = [val];

                    break;
                case JsonValueKind.Array:
                    model = element.Deserialize(GameModInfoModelContext.Default.GameModInfoModelArray) ?? [];
                    break;
            }

            if (model == null || model.Length == 0)
                return null;

            var authors = new HashSet<string>();
            foreach (var author in model.Where(m => m.AuthorList != null).SelectMany(m => m.AuthorList!))
                authors.Add(author);

            var baseMod = model.FirstOrDefault(m => string.IsNullOrEmpty(m.Parent)) ?? model.First();

            var authorStr = string.Join(',', authors);
            var authorResult = string.IsNullOrEmpty(authorStr) ? null : authorStr;
            var modList = model
                .Where(m => !string.IsNullOrEmpty(m.Parent) && m != baseMod)
                .Where(m => !string.IsNullOrEmpty(m.Name))
                .Select(m => m.Name!)
                .ToImmutableList();
            var titleResult = string.IsNullOrEmpty(baseMod.Name) ? Path.GetFileName(file) : baseMod.Name;

            var displayModel = new GameModResolvedInfo(authorResult, file, modList, titleResult, baseMod.Version,
                "Forge *", isEnabled);

            return displayModel;
        }
        catch (JsonRepairError)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static async Task<GameModResolvedInfo> GetFabricModInfo(
        IArchiveEntry entry,
        string file,
        bool isEnabled,
        CancellationToken ct)
    {
        try
        {
            await using var stream = entry.OpenEntryStream();
            var tempModel = await JsonSerializer.DeserializeAsync(stream,
                FabricModInfoModelContext.Default.FabricModInfoModel, ct);

            var author = tempModel?.Authors is { Length: > 0 }
                ? string.Join(',', tempModel.Authors)
                : null;
            var modList = tempModel?.Depends?.Select(d => d.Key)?.ToImmutableList();
            var titleResult = string.IsNullOrEmpty(tempModel?.Id) ? Path.GetFileName(file) : tempModel.Id;
            var versionResult = string.IsNullOrEmpty(tempModel?.Version) ? null : tempModel.Version;

            return new GameModResolvedInfo(author, file, modList, titleResult, versionResult, "Fabric", isEnabled);
        }
        catch (JsonException e)
        {
            var errorList = new[]
            {
                "[!] 数据包 JSON 异常",
                e.Message
            };
            return new GameModResolvedInfo(null, file, errorList.ToImmutableList(), Path.GetFileName(file), null,
                "Fabric", isEnabled);
        }
    }

    public static ModLoaderType GetModLoaderType(IArchive archive)
    {
        var fabricEntry = archive.Entries.Any(e =>
            e.Key?.EndsWith("fabric.mod.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (fabricEntry) return ModLoaderType.Fabric;

        var neoforgeEntry = archive.Entries.Any(e =>
            e.Key?.EndsWith("_neoforge.mixins.json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (neoforgeEntry) return ModLoaderType.NeoForge;

        var forgeEntry = archive.Entries.Any(e =>
            e.Key?.EndsWith("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase) ?? false);
        var forgeNewEntry = archive.Entries.Any(e =>
            e.Key?.EndsWith("mcmod.info", StringComparison.OrdinalIgnoreCase) ?? false);

        if (forgeEntry || forgeNewEntry) return ModLoaderType.Forge;

        return ModLoaderType.Unknown;
    }

    private static async Task<byte[]?> TryResolveModIcon(IArchive archive)
    {
        static bool IsInRootPath(IArchiveEntry entry)
        {
            var path = entry.Key?.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (path == null || path.Length == 0) return false;

            return path.Length == 1;
        }
        
        static bool IsImageFile(IArchiveEntry entry)
        {
            var ext = Path.GetExtension(entry.Key ?? string.Empty);

            return !string.IsNullOrEmpty(ext) &&
                   (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".webp", StringComparison.OrdinalIgnoreCase));
        }
        
        var iconEntries = archive.Entries
            .Where(e => IsImageFile(e) && IsInRootPath(e));
        var iconEntry = iconEntries
            .FirstOrDefault(e => e.Key!.Contains("icon", StringComparison.OrdinalIgnoreCase) ||
                                 e.Key.Contains("logo", StringComparison.OrdinalIgnoreCase) ||
                                 e.Key.Contains("cover", StringComparison.OrdinalIgnoreCase) ||
                                 e.Key.Contains("banner", StringComparison.OrdinalIgnoreCase))
            ?? iconEntries.FirstOrDefault();
        
        if (iconEntry == null)
            return null;

        await using var entryStream = iconEntry.OpenEntryStream();
        await using var ms = new MemoryStream();
        
        await entryStream.CopyToAsync(ms);
        
        return ms.ToArray();
    }

    public static async IAsyncEnumerable<GameModResolvedInfo> ResolveModListAsync(
        IEnumerable<string> files,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) yield break;

            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) ||
                !(ext.Equals(".jar", StringComparison.OrdinalIgnoreCase) ||
                  ext.Equals(".disabled", StringComparison.OrdinalIgnoreCase)))
                continue;

            await using var fs = File.OpenRead(file);

            if (!ArchiveHelper.TryOpen(fs, out var archive)) continue;

            var modInfoEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("mcmod.info", StringComparison.OrdinalIgnoreCase) ?? false);
            var fabricModInfoEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("fabric.mod.json", StringComparison.OrdinalIgnoreCase) ?? false);
            var tomlInfoEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase) ?? false);
            var neoforgeTomlInfoEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.Key?.Equals("META-INF/neoforge.mods.toml", StringComparison.OrdinalIgnoreCase) ?? false);

            var isEnabled = ext.Equals(".jar", StringComparison.OrdinalIgnoreCase);

            GameModResolvedInfo? result = null;

            try
            {
                if (modInfoEntry != null)
                {
                    result = await GetNewModInfo(modInfoEntry, file, isEnabled, ct);

                    if (result != null) goto ReturnResult;
                }

                if (tomlInfoEntry != null || neoforgeTomlInfoEntry != null)
                {
                    var toml = tomlInfoEntry ?? neoforgeTomlInfoEntry;

                    result = await GetTomlModInfo(toml!, file, isEnabled, toml == neoforgeTomlInfoEntry);

                    if (result != null) goto ReturnResult;
                }

                if (fabricModInfoEntry != null)
                {
                    result = await GetFabricModInfo(fabricModInfoEntry, file, isEnabled, ct);
                    goto ReturnResult;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                goto ReturnResult;
            }

            result ??= new GameModResolvedInfo(
                null,
                file,
                ["[!] 未知的数据包类型"],
                Path.GetFileName(file),
                null,
                "Unknown",
                isEnabled);

            ReturnResult:
            result = result! with
            {
                LoaderType = GetModLoaderType(archive),
                IconBytes = await TryResolveModIcon(archive)
            };
            yield return result;
        }
    }

    static async Task<GameResourcePackResolvedInfo?> ResolveResPackFile(
        string file,
        CancellationToken ct)
    {
        var ext = Path.GetExtension(file);

        if (!ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) return null;

        await using var fs = File.OpenRead(file);

        if (!ArchiveHelper.TryOpen(fs, out var archive)) return null;

        var packIconEntry =
            archive.Entries.FirstOrDefault(e => e.Key?.Equals("pack.png", StringComparison.OrdinalIgnoreCase) ?? false);
        var packInfoEntry = archive.Entries.FirstOrDefault(e =>
            e.Key?.Equals("pack.mcmeta", StringComparison.OrdinalIgnoreCase) ?? false);

        var fileName = Path.GetFileName(file);
        byte[]? imageBytes;
        string? description = null;
        var version = -1;

        if (packIconEntry != null)
        {
            await using var stream = packIconEntry.OpenEntryStream();
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);

            imageBytes = ms.ToArray();
        }
        else
        {
            return null;
        }

        if (packInfoEntry != null)
            try
            {
                await using var stream = packInfoEntry.OpenEntryStream();
                var model = await JsonSerializer.DeserializeAsync(stream,
                    GameResourcePackModelContext.Default.GameResourcePackModel, ct);

                description = model?.Pack?.Description;
                version = model?.Pack?.PackFormat ?? -1;
            }
            catch (JsonException e)
            {
                description = $"[!] 数据包 JSON 异常: {e.Message}";
                version = -1;
            }

        return new GameResourcePackResolvedInfo(fileName, description, version, imageBytes);
    }

    static async Task<GameResourcePackResolvedInfo?> ResolveResPackDir(
        string dir,
        CancellationToken ct)
    {
        var iconPath = Path.Combine(dir, "pack.png");
        var infoPath = Path.Combine(dir, "pack.mcmeta");

        if (!File.Exists(iconPath)) return null;

        var fileName = Path.GetFileName(dir);
        var imageBytes = await File.ReadAllBytesAsync(iconPath, ct);
        string? description = null;
        var version = -1;

        if (File.Exists(infoPath))
            try
            {
                await using var contentStream = File.OpenRead(infoPath);
                var model = await JsonSerializer.DeserializeAsync(contentStream,
                    GameResourcePackModelContext.Default.GameResourcePackModel, ct);

                description = model?.Pack?.Description;
                version = model?.Pack?.PackFormat ?? -1;
            }
            catch (JsonException e)
            {
                description = $"[!] 数据包 JSON 异常: {e.Message}";
                version = -1;
            }

        return new GameResourcePackResolvedInfo(fileName, description, version, imageBytes);
    }

    public static async IAsyncEnumerable<GameResourcePackResolvedInfo> ResolveResourcePackAsync(
        IEnumerable<(string, bool)> files,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var (path, isDir) in files)
        {
            if (ct.IsCancellationRequested) yield break;

            if (!isDir)
            {
                var result = await ResolveResPackFile(path, ct);

                if (result == null) continue;

                yield return result;
            }
            else
            {
                var result = await ResolveResPackDir(path, ct);

                if (result == null) continue;

                yield return result;
            }
        }
    }

    static async Task<GameShaderPackResolvedInfo?> ResolveShaderPackFile(string file)
    {
        await using var fs = File.OpenRead(file);

        if (!ArchiveHelper.TryOpen(fs, out var archive)) return null;
        if (!archive.Entries.Any(e =>
                Path.GetFileName(e.Key?.TrimEnd('/'))
                    ?.Equals("shaders", StringComparison.OrdinalIgnoreCase) ?? false))
            return null;

        var model = new GameShaderPackResolvedInfo(Path.GetFileName(file), false);

        return model;
    }

    static GameShaderPackResolvedInfo? ResolveShaderPackDir(string dir)
    {
        var shaderPath = Path.Combine(dir, "shaders");

        if (!Directory.Exists(shaderPath)) return null;

        return new GameShaderPackResolvedInfo(Path.GetFileName(dir), true);
    }

    public static async IAsyncEnumerable<GameShaderPackResolvedInfo> ResolveShaderPack(
        IEnumerable<(string, bool)> paths,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var (path, isDir) in paths)
        {
            if (ct.IsCancellationRequested) yield break;

            if (!isDir)
            {
                var result = await ResolveShaderPackFile(path);

                if (result == null) continue;

                yield return result;
            }
            else
            {
                var result = ResolveShaderPackDir(path);

                if (result == null) continue;

                yield return result;
            }
        }
    }

    public static IEnumerable<GameScreenshotResolvedInfo> ResolveScreenshot(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;

            var extension = Path.GetExtension(file);

            if (string.IsNullOrEmpty(extension)) continue;
            if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)) continue;

            var fileName = Path.GetFileName(file);

            yield return new GameScreenshotResolvedInfo(file, fileName);
        }
    }
}