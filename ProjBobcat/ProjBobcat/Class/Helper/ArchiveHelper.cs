using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using SharpCompress.Archives;

namespace ProjBobcat.Class.Helper;

public static class ArchiveHelper
{
    public static bool TryOpen(string path, [MaybeNullWhen(false)] out IArchive archive)
    {
        try
        {
            archive = ArchiveFactory.Open(path);
        }
        catch (Exception)
        {
            archive = null;
            return false;
        }

        return true;
    }

    public static bool TryOpen(FileInfo path, [MaybeNullWhen(false)] out IArchive archive)
    {
        try
        {
            archive = ArchiveFactory.Open(path);
        }
        catch (Exception)
        {
            archive = null;
            return false;
        }

        return true;
    }

    public static bool TryOpen(Stream path, [MaybeNullWhen(false)] out IArchive archive)
    {
        try
        {
            archive = ArchiveFactory.Open(path);
        }
        catch (Exception)
        {
            archive = null;
            return false;
        }

        return true;
    }
}