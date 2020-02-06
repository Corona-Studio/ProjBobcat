namespace ProjBobcat.Class.Helper
{
    public static class GamePathHelper
    {
        public static string GetVersionPath(string rootPath)
        {
            return $"{rootPath}\\versions\\";
        }

        public static string GetGamePath(string rootPath, string id)
        {
            return $"{rootPath}\\versions\\{id}";
        }

        public static string GetGameJsonPath(string rootPath, string id)
        {
            return string.Format("{0}\\versions\\{1}\\{1}.json", rootPath, id);
        }

        public static string GetGameExecutablePath(string rootPath, string id)
        {
            return string.Format("{0}\\versions\\{1}\\{1}.jar", rootPath, id);
        }

        public static string GetLibraryPath(string rootPath, string libraryPath)
        {
            return $"{rootPath}\\libraries\\{libraryPath}";
        }

        public static string GetNativeRoot(string rootPath, string versionId)
        {
            return $"{rootPath}\\versions\\{versionId}\\natives";
        }

        public static string GetVersionJar(string rootPath, string versionId)
        {
            return string.Format("{0}\\versions\\{1}\\{1}.jar", rootPath, versionId);
        }

        public static string GetAssetsRoot(string rootPath)
        {
            return $"{rootPath}\\assets";
        }

        public static string GetLauncherProfilePath(string rootPath)
        {
            return $"{rootPath}\\launcher_profiles.json";
        }
    }
}