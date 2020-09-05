using System.IO;

namespace ProjBobcat.Class.Helper
{
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
    }
}