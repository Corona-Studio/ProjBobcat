using System;
using System.IO;
using SharpCompress.Archives;

namespace ProjBobcat.Class.Helper;

#nullable enable

public static class ArchiveHelper
{
    public static bool TryOpen(string path, out IArchive? archive)
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

    public static bool TryOpen(FileInfo path, out IArchive? archive)
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

    public static bool TryOpen(Stream path, out IArchive? archive)
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