﻿using System;
using System.IO;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     包含一些和游戏文件位置有关的方法。
///     Contains a few methods related to games' location.
/// </summary>
public static class GamePathHelper
{
    public static string GetLoggingPath(string rootPath)
    {
        return Path.Combine(rootPath, "logging");
    }

    /// <summary>
    ///     .minecraft/versions/
    /// </summary>
    /// <returns></returns>
    public static string GetVersionPath(string rootPath)
    {
        return Path.Combine(rootPath, "versions");
    }

    /// <summary>
    ///     .minecraft/versions/{id}
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static string GetGamePath(string id)
    {
        return Path.Combine("versions", id);
    }

    /// <summary>
    ///     .minecraft/versions/{id}/{id}.json
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static string GetGameJsonPath(string rootPath, string id)
    {
        var versions = Path.Combine(rootPath, "versions", id);

        if (!Directory.Exists(versions))
            return Path.Combine(versions, $"{id}.json");

        foreach (var file in Directory.EnumerateFiles(versions, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if (name.Contains(id, StringComparison.OrdinalIgnoreCase))
                return file;

            if (id.Contains(name, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return Path.Combine(versions, $"{id}.json");
    }

    /// <summary>
    ///     .minecraft/versions/{id}/{id}.jar
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static string GetGameExecutablePath(string id)
    {
        return Path.Combine("versions", id, $"{id}.jar");
    }

    /// <summary>
    ///     .minecraft/libraries/{libraryPath}/
    /// </summary>
    /// <param name="libraryPath"></param>
    /// <returns></returns>
    public static string GetLibraryPath(string libraryPath)
    {
        return Path.Combine("libraries", libraryPath);
    }

    /// <summary>
    ///     .minecraft/libraries/
    /// </summary>
    /// <returns></returns>
    public static string GetLibraryRootPath()
    {
        return "libraries";
    }

    /// <summary>
    ///     .minecraft/versions/natives/
    /// </summary>
    /// <param name="versionId"></param>
    /// <returns></returns>
    public static string GetNativeRoot(string versionId)
    {
        return Path.Combine("versions", versionId, "natives");
    }

    /// <summary>
    ///     .minecraft/versions/{versionId}/{versionId}.jar
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="versionId"></param>
    /// <returns></returns>
    public static string GetVersionJar(string rootPath, string versionId)
    {
        return Path.Combine(rootPath, "versions", versionId, $"{versionId}.jar");
    }

    /// <summary>
    ///     .minecraft/versions/{versionId}/options.txt
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="versionId"></param>
    /// <returns></returns>
    public static string GetVersionConfig(string versionId)
    {
        return Path.Combine("versions", versionId, "options.txt");
    }

    /// <summary>
    ///     .minecraft/assets/
    /// </summary>
    /// <returns></returns>
    public static string GetAssetsRoot()
    {
        return "assets";
    }

    /// <summary>
    ///     .minecraft/launcher_profiles.json
    /// </summary>
    /// <returns></returns>
    public static string GetLauncherProfilePath()
    {
        return "launcher_profiles.json";
    }

    /// <summary>
    ///     .minecraft/launcher_accounts.json
    /// </summary>
    /// <returns></returns>
    public static string GetLauncherAccountPath()
    {
        return "launcher_accounts.json";
    }

    /// <summary>
    ///     官方启动器的 .minecraft 目录
    /// </summary>
    /// <returns></returns>
    public static string OfficialLauncherGamePath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library",
                "Application Support", ".minecraft");
        if (OperatingSystem.IsLinux())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".minecraft");
        return string.Empty;
    }
}