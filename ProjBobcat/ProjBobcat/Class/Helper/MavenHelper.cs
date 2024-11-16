using System;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     Maven解析工具类
/// </summary>
public static partial class MavenHelper
{
    [GeneratedRegex("\\.")]
    private static partial Regex GroupPathRegex();

    /// <summary>
    ///     使用名字来解析Maven包信息。
    ///     Parse Maven package's info with its name.
    /// </summary>
    /// <param name="mavenString">Maven包的名称。Maven package's name.</param>
    /// <returns></returns>
    public static MavenInfo? ResolveMavenString(this string mavenString)
    {
        if (string.IsNullOrEmpty(mavenString))
            return null;

        #region '@'处理器 '@' Processor

        // 一些安装有forge的游戏版本的Maven名包含@符号。此时type被包含在第二项。
        // A few forge game version's maven's name contains @ symbol.
        var rawArray = mavenString.Split('@');
        var type = "jar";
        if (rawArray.Length == 2) type = rawArray[1];

        #endregion

        var classifier = string.Empty;
        var mavenArr = rawArray[0].Split(':');

        if (mavenArr.Length == 4) classifier = mavenArr[3];

        var orgName = mavenArr[0];
        var artifactId = mavenArr[1];
        var version = mavenArr[2];
        var isSnapshot = mavenArr[2].EndsWith("-SNAPSHOT", StringComparison.OrdinalIgnoreCase);

        var artifactPath = orgName.GetGroupPath();
        var basePath = $"{artifactPath}/{artifactId}/{version}/{artifactId}-{version}";
        if (!string.IsNullOrEmpty(classifier)) basePath += $"-{classifier}";

        var fullPath = $"{basePath}.{type}";
        return new MavenInfo
        {
            ArtifactId = artifactId,
            Classifier = classifier,
            IsSnapshot = isSnapshot,
            OrganizationName = orgName,
            Type = type,
            Version = version,
            Path = fullPath
        };
    }

    /// <summary>
    ///     获取Group Path
    /// </summary>
    /// <param name="artifactId">Maven Id</param>
    /// <returns>处理好的Group Path</returns>
    public static string GetGroupPath(this string artifactId)
    {
        return GroupPathRegex().Replace(artifactId, "/");
    }

    /// <summary>
    ///     获取Maven全名
    /// </summary>
    /// <param name="mavenInfo">Maven 信息实例</param>
    /// <returns>拼接出来的Maven全称</returns>
    public static string GetMavenFullName(this MavenInfo? mavenInfo)
    {
        return mavenInfo is null
            ? string.Empty
            : string.IsNullOrEmpty(mavenInfo.Classifier)
                ? $"{mavenInfo.OrganizationName}.{mavenInfo.ArtifactId}"
                : $"{mavenInfo.OrganizationName}.{mavenInfo.ArtifactId}.{mavenInfo.Classifier}";
    }
}