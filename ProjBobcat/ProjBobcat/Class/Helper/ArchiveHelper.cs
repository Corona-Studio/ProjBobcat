using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

public static class ArchiveHelper
{
    private const int ValidZipDateYearMin = 1980;
    private const int ValidZipDateYearMax = 2107;
    
    public static bool IsDirectory(this ZipArchiveEntry entry)
    {
        return entry.Length == 0 && entry.FullName.EndsWith('/');
    }

    public static void AddEntry(this ZipArchive archive, string entryName, Stream stream, DateTime time, CompressionLevel level = CompressionLevel.SmallestSize)
    {
        var entry = archive.CreateEntry(entryName, level);
        
        entry.LastWriteTime = time.Year switch
        {
            < ValidZipDateYearMin or > ValidZipDateYearMax => DateTime.Now,
            _ => time
        };

        using var entryStream = entry.Open();

        stream.CopyTo(entryStream);
    }

    public static void AddEntry(this ZipArchive archive, string entryName, Stream stream, CompressionLevel level = CompressionLevel.SmallestSize)
    {
        var entry = archive.CreateEntry(entryName, level);
        using var entryStream = entry.Open();

        stream.CopyTo(entryStream);
    }

    public static async Task AddEntryAsync(this ZipArchive archive, string entryName, string filePath, CompressionLevel level = CompressionLevel.SmallestSize)
    {
        var entry = archive.CreateEntry(entryName, level);

        await using var fs = File.OpenRead(filePath);
        await using var entryStream = entry.Open();

        await fs.CopyToAsync(entryStream);
    }

    public static async Task AddEntryAsync(this ZipArchive archive, string entryName, Stream stream, CompressionLevel level = CompressionLevel.SmallestSize)
    {
        var entry = archive.CreateEntry(entryName, level);
        await using var entryStream = entry.Open();

        await stream.CopyToAsync(entryStream);
    }

    public static bool TryOpenRead(Stream stream, [MaybeNullWhen(false)] out ZipArchive archive)
    {
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (ArgumentException)
        {
            archive = null;
            return false;
        }
        catch (InvalidDataException)
        {
            archive = null;
            return false;
        }

        return true;
    }
}