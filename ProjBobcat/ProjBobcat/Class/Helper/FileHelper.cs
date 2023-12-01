using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     文件操作帮助器。
/// </summary>
public static class FileHelper
{
    public static FileType GetFileType(string path)
    {
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);

        var b = new byte[2];
        var buffer = reader.ReadByte();

        b[0] = buffer;

        var fileClass = buffer.ToString();

        buffer = reader.ReadByte();
        b[1] = buffer;
        fileClass += buffer.ToString();

        var type = Enum.TryParse(fileClass, out FileType t) ? t : FileType.ValidFile;
        return type;
    }

    public static async Task<FileStream?> OpenReadAsync(string path, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                return File.OpenRead(path);
            }
            catch (IOException)
            {
                await Task.Delay(1000, token);
            }
        }
        
        return null;
    }
}