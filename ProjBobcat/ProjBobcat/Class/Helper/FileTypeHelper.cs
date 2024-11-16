using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using SharpCompress.Archives;

namespace ProjBobcat.Class.Helper;

public static class FileTypeHelper
{
    public static async Task<AssetFileType> TryDetectFileTypeAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new IOException("File not found.");

        var extension = Path.GetExtension(filePath);

        switch (extension)
        {
            case ".mrpack":
                return AssetFileType.ModrinthModPack;
            case ".zip":
            {
                await using var fs = File.OpenRead(filePath);
                using var archive = ArchiveFactory.Open(fs);

                if (archive.Entries.Any(
                        e => e.Key?.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ?? false))
                    return AssetFileType.CurseForgeModPack;
                if (archive.Entries.Any(e =>
                        e.Key?.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase) ?? false))
                    return AssetFileType.ModrinthModPack;
                break;
            }
        }

        return AssetFileType.Unknown;
    }
}