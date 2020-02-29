﻿namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// 包含一些和游戏文件位置有关的方法。
    /// Contains a few methods related to games' location.
    /// </summary>
    public static class GamePathHelper
    {
        /// <summary>
        /// .minecraft/versions/
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <returns></returns>
        public static string GetVersionPath(string rootPath) => $"{rootPath}versions\\";

        /// <summary>
        /// .minecraft/versions/{id}
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetGamePath(string rootPath, string id) => $"{rootPath}versions\\{id}";

        /// <summary>
        /// .minecraft/versions/{id}/{id}.json
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetGameJsonPath(string rootPath, string id) => $"{rootPath}versions\\{id}\\{id}.json";

        /// <summary>
        /// .minecraft/versions/{id}/{id}.jar
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetGameExecutablePath(string rootPath, string id) => $"{rootPath}versions\\{id}\\{id}.jar";

        /// <summary>
        /// .minecraft/libraries/{libraryPath}/
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="libraryPath"></param>
        /// <returns></returns>
        public static string GetLibraryPath(string rootPath, string libraryPath) => $"{rootPath}libraries\\{libraryPath}";

        /// <summary>
        /// .minecraft/libraries/{libraryPath}/
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <returns></returns>
        public static string GetLibraryRootPath(string rootPath) => $"{rootPath}libraries";

        /// <summary>
        /// .minecraft/versions/natives/
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="versionId"></param>
        /// <returns></returns>
        public static string GetNativeRoot(string rootPath, string versionId) => $"{rootPath}versions\\{versionId}\\natives";

        /// <summary>
        /// .minecraft/versions/{versionId}/{versionId}.jar
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <param name="versionId"></param>
        /// <returns></returns>
        public static string GetVersionJar(string rootPath, string versionId) => $"{rootPath}versions\\{versionId}\\{versionId}.jar";

        /// <summary>
        /// .minecraft/assets/
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <returns></returns>
        public static string GetAssetsRoot(string rootPath) => $"{rootPath}assets";

        /// <summary>
        /// .minecraft/launcher_profiles.json
        /// </summary>
        /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
        /// <returns></returns>
        public static string GetLauncherProfilePath(string rootPath) => $"{rootPath}launcher_profiles.json";
    }
}