using System.IO;

namespace ProjBobcat.Class.Helper
{
    public static class DirectoryHelper
    {
        public static void CleanDirectory(string path, bool deleteDirectory)
        {
            var directory = new DirectoryInfo(path);
            foreach (var file in directory.GetFiles()) file.Delete();
            foreach (var subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
            if (deleteDirectory) directory.Delete();
        }
    }
}