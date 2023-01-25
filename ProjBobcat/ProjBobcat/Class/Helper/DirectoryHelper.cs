using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     目录操作帮助器。
/// </summary>
public static class DirectoryHelper
{
    /// <summary>
    ///     清空目录。
    /// </summary>
    /// <param name="path">目录路径。</param>
    /// <param name="deleteDirectory">指示是否要删除目录本身。</param>
    public static void CleanDirectory(string path, bool deleteDirectory = false)
    {
        var directory = new DirectoryInfo(path);
        if (deleteDirectory)
        {
            directory.Delete(true);
        }
        else
        {
            foreach (var file in directory.GetFiles()) file.Delete();
            foreach (var subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        }
    }

    /// <summary>
    ///     获取一个目录下所有文件和文件夹
    /// </summary>
    /// <param name="path">需要获取的路径</param>
    /// <returns>（路径，是否是文件夹）</returns>
    public static IEnumerable<(string, bool)> EnumerateFilesAndDirectories(string path)
    {
        var files = Directory.EnumerateFiles(path).Select(p => (p, false));
        var dirs = Directory.EnumerateDirectories(path).Select(p => (p, true));

        return files.Concat(dirs);
    }
}