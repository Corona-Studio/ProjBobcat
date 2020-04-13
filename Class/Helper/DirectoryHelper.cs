using System.IO;

namespace ProjBobcat.Class.Helper
{
    public static class DirectoryHelper
    {
        public static void CleanDirectory(string path, bool deleteDirectory = false)
        {
            var directory = new DirectoryInfo(path);
            if (deleteDirectory)
                directory.Delete(true);
            else
            {
                foreach (var file in directory.GetFiles()) file.Delete();
                foreach (var subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
            }
        }
    }
}