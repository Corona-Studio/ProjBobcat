using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     文件操作帮助器。
/// </summary>
public static class FileHelper
{
    public static async Task<FileStream?> OpenReadAsync(string path, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
            try
            {
                return File.OpenRead(path);
            }
            catch (IOException)
            {
                await Task.Delay(1000, token);
            }

        return null;
    }
}