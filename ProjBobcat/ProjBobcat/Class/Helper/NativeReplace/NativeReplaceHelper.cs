using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.JsonContexts;

namespace ProjBobcat.Class.Helper.NativeReplace;

public static partial class NativeReplaceHelper
{
    static readonly NativeReplaceModel NativeReplaceModel;

    static NativeReplaceHelper()
    {
        var model = JsonSerializer.Deserialize(ReplaceDicJson, NativeReplaceModelContext.Default.NativeReplaceModel);

        ArgumentNullException.ThrowIfNull(model);

        NativeReplaceModel = model;
    }

    static string GetNativeKey()
    {
        var platform = NativeReplaceModel switch
        {
            _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "windows",
            _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux",
            _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx",
            _ when RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) => "freebsd",
            _ => string.Empty
        };

        var arch = NativeReplaceModel switch
        {
            _ when RuntimeInformation.OSArchitecture == Architecture.X64 => "x86_64",
            _ when RuntimeInformation.OSArchitecture == Architecture.X86 => "x86",
            _ when RuntimeInformation.OSArchitecture == Architecture.Arm64 => "arm64",
            _ when RuntimeInformation.OSArchitecture == Architecture.Arm => "arm32",
            _ when RuntimeInformation.OSArchitecture == Architecture.LoongArch64 => "loongarch64",
            _ => string.Empty
        };

        return $"{platform}-{arch}";
    }

    public static List<Library> Replace(List<RawVersionModel> versions, List<Library> libs,
        NativeReplacementPolicy policy)
    {
        if (policy == NativeReplacementPolicy.Disabled) return libs;

        var mcVersion = GameVersionHelper.TryGetMcVersion(versions);

        if (string.IsNullOrEmpty(mcVersion) && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        var versionsArr = mcVersion?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = -1;

        if (versionsArr is { Length: >= 2 }) minor = int.TryParse(versionsArr[1], out var outMinor) ? outMinor : -1;

        if (minor is -1 or >= 19 && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        var replaceKey = GetNativeKey();
        var replaceDic = replaceKey switch
        {
            "windows-x86_64" => NativeReplaceModel.WindowsX64,
            "windows-x86" => NativeReplaceModel.WindowsX86,
            "windows-arm64" => NativeReplaceModel.WindowsArm64,
            "linux-arm64" => NativeReplaceModel.LinuxArm64,
            "linux-arm32" => NativeReplaceModel.LinuxArm86,
            "linux-loongarch64" => NativeReplaceModel.LinuxLoongArch64,
            "linux-loongarch64_ow" => NativeReplaceModel.LinuxLoongArch64Ow,
            "osx-arm64" => NativeReplaceModel.OsxArm64,
            "freebsd-x86_64" => NativeReplaceModel.FreeBsdX64,
            _ => null
        };

        if (replaceDic == null) return libs;

        var replaced = new List<Library>();
        var filtered = libs.Where(lib => IsNotNative(lib.Name)).ToList();

        foreach (var original in filtered)
        {
            var originalMaven = original.Name.ResolveMavenString();

            if (originalMaven == null)
            {
                replaced.Add(original);
                continue;
            }

            var isNative = original.IsNewNativeLib() ||
                           (original.Natives?.Count ?? 0) > 0 ||
                           original.Downloads?.Classifiers != null;
            var candidateKey = isNative
                ? $"{originalMaven.OrganizationName}:{originalMaven.ArtifactId}:{originalMaven.Version}:natives"
                : original.Name;

            if (!replaceDic.TryGetValue(candidateKey, out var candidate))
            {
                replaced.Add(original);
                continue;
            }

            if (candidate == null)
            {
                replaced.Add(original);
                continue;
            }

            if (candidate.Downloads?.Artifact != null &&
                !(candidate.Downloads.Artifact.Url?.StartsWith("[X]", StringComparison.OrdinalIgnoreCase) ?? false))
                candidate.Downloads.Artifact.Url = $"[X]{candidate.Downloads.Artifact.Url}";
            if (candidate.Downloads?.Classifiers != null)
                foreach (var (_, fi) in candidate.Downloads.Classifiers)
                {
                    if (fi.Url?.StartsWith("[X]", StringComparison.OrdinalIgnoreCase) ?? false) continue;
                    fi.Url = $"[X]{fi.Url}";
                }

            replaced.Add(candidate);
        }

        return replaced;

        static bool IsNotNative(string libName)
        {
            var maven = libName.ResolveMavenString();

            if (maven == null) return true;

            var isLwjgl = maven.OrganizationName == "org.lwjgl";
            var isNative = maven.Classifier.StartsWith("natives", StringComparison.OrdinalIgnoreCase);
            var isSpecialLwjglLib = maven.ArtifactId is "lwjgl-glfw" or "lwjgl-openal";

            return !(isLwjgl && isNative && isSpecialLwjglLib);
        }
    }
}