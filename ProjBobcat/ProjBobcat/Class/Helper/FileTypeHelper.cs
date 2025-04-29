using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

public static class FileTypeHelper
{
    public static async Task<AssetFileType> TryDetectFileTypeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(filePath);

        var extension = Path.GetExtension(filePath);

        switch (extension)
        {
            case ".mrpack":
                return AssetFileType.ModrinthModPack;
            case ".zip":
            {
                await using var fs = File.OpenRead(filePath);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

                if (archive.Entries.Any(e => e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)))
                    return AssetFileType.CurseForgeModPack;
                if (archive.Entries.Any(e =>
                        e.FullName.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase)))
                    return AssetFileType.ModrinthModPack;
                break;
            }
        }

        return AssetFileType.Unknown;
    }
}