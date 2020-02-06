using ProjBobcat.Class.Model;
using System;
using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper
{
    public static class MavenHelper
    {
        public static MavenInfo ResolveMavenString(this string mavenString)
        {
            if (string.IsNullOrEmpty(mavenString))
                return null;

            var rawArray = mavenString.Split('@');
            var type = "jar";
            if (rawArray.Length == 2)
            {
                type = rawArray[1];
            }

            var classifier = string.Empty;
            var mavenArr = rawArray[0].Split(':');

            if (mavenArr.Length == 4)
            {
                classifier = mavenArr[3];
            }

            var orgName = mavenArr[0];
            var artifactId = mavenArr[1];
            var version = mavenArr[2];
            var isSnapshot = mavenArr[2].EndsWith("-SNAPSHOT", StringComparison.OrdinalIgnoreCase);

            var artifactPath = orgName.GetGroupPath();
            var basePath = isSnapshot
                ? $"{artifactPath}/{artifactId}/{version}/{version}"
                : $"{artifactPath}/{artifactId}/{version}/{artifactId}-{version}";
            if (!string.IsNullOrEmpty(classifier))
            {
                basePath += $"-{classifier}";
            }

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

        public static string GetGroupPath(this string artifactId)
        {
            var regex = new Regex("\\.");
            return regex.Replace(artifactId, "/");
        }

        public static string GetMavenFullName(this MavenInfo mavenInfo)
        {
            if (mavenInfo == null || mavenInfo.Equals(default(MavenInfo)))
                return string.Empty;

            return $"{mavenInfo.OrganizationName}.{mavenInfo.ArtifactId}";
        }
    }
}