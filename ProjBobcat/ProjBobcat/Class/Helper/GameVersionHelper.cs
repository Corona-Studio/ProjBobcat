using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

public static partial class GameVersionHelper
{
    [GeneratedRegex(@"1.\d{1,2}(.\d{1,2})*")]
    private static partial Regex McVersionMatch();

    public static string? TryGetMcVersion(List<RawVersionModel> versions)
    {
        foreach (var version in versions)
        {
            var mcVersion = TryGetMcVersionByForgeGameArgs(version) ??
                            TryGetMcVersionByFabric(version) ??
                            TryGetMcVersionByOptifine(version) ??
                            TryGetMcVersionByInheritFrom(version) ??
                            TryGetMcVersionByClientVersion(version) ??
                            TryGetMcVersionById(version);

            if (string.IsNullOrEmpty(mcVersion)) continue;
            if (!McVersionMatch().IsMatch(mcVersion)) continue;

            return mcVersion;
        }

        return null;
    }

    static string TryGetMcVersionById(RawVersionModel version)
    {
        return version.Id;
    }

    static string? TryGetMcVersionByClientVersion(RawVersionModel version)
    {
        return version.ClientVersion;
    }

    static string? TryGetMcVersionByInheritFrom(RawVersionModel version)
    {
        return version.InheritsFrom;
    }

    static string? TryGetMcVersionByForgeGameArgs(RawVersionModel version)
    {
        var gameArgs = version.Arguments?.Game?
            .Where(arg => arg.ValueKind == JsonValueKind.String)
            .Select(arg => arg.GetString())
            .OfType<string>()
            .ToList();

        if (gameArgs == null) return null;

        var containsForgeArgs = gameArgs.Contains("--fml.forgeVersion", StringComparer.OrdinalIgnoreCase) &&
                                gameArgs.Contains("--fml.mcVersion", StringComparer.OrdinalIgnoreCase);

        if (!containsForgeArgs) return null;

        var mcVersion = gameArgs[gameArgs.IndexOf("--fml.mcVersion") + 1];

        return mcVersion;
    }

    static string? TryGetMcVersionByFabric(RawVersionModel version)
    {
        const string fabricLibPrefix = "net.fabricmc:intermediary:";

        var fabricLib = version.Libraries
            .FirstOrDefault(lib => lib.Name.StartsWith(fabricLibPrefix, StringComparison.OrdinalIgnoreCase));

        var mcVersion = fabricLib?.Name[fabricLibPrefix.Length..];

        return mcVersion;
    }

    static string? TryGetMcVersionByOptifine(RawVersionModel version)
    {
        const string optifineLibPrefix = "optifine:OptiFine:";

        var optifineLib = version.Libraries
            .FirstOrDefault(lib => lib.Name.StartsWith(optifineLibPrefix, StringComparison.OrdinalIgnoreCase));

        var mcVersion = optifineLib?.Name[optifineLibPrefix.Length..]
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return mcVersion;
    }
}