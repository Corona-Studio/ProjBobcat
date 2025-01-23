using System;
using System.Collections.Generic;
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

    public static List<Library> Replace(
        List<RawVersionModel> versions,
        List<Library> libs,
        NativeReplacementPolicy policy,
        bool useSystemGlfwOnLinux,
        bool useSystemOpenAlOnLinux)
    {
        if (policy == NativeReplacementPolicy.Disabled) return libs;

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

        var replaced = new List<Library>();

        // Replace special LWJGL and OpenAL libraries on Linux
        if (OperatingSystem.IsLinux() && (useSystemGlfwOnLinux || useSystemOpenAlOnLinux))
        {
            foreach (var original in libs)
            {
                var originalMaven = original.Name.ResolveMavenString();

                if (originalMaven == null) continue;
                if (!string.IsNullOrEmpty(originalMaven.Classifier) &&
                    originalMaven.Classifier.StartsWith("natives", StringComparison.OrdinalIgnoreCase) &&
                    originalMaven.OrganizationName.Equals("org.lwjgl", StringComparison.OrdinalIgnoreCase))
                {
                    if (useSystemGlfwOnLinux &&
                        !originalMaven.ArtifactId.Equals("lwjgl-glfw", StringComparison.OrdinalIgnoreCase))
                    {
                        replaced.Add(original);
                        continue;
                    }

                    if (useSystemOpenAlOnLinux &&
                        !originalMaven.ArtifactId.Equals("lwjgl-openal", StringComparison.OrdinalIgnoreCase))
                    {
                        replaced.Add(original);
                    }
                }
            }
        }

        var osCheckFlag = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

        if (RuntimeInformation.ProcessArchitecture == Architecture.X86 && osCheckFlag)
            return libs;

        var isNotLinux = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        var mcVersion = GameVersionHelper.TryGetMcVersion(versions);

        if (string.IsNullOrEmpty(mcVersion) && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        var versionsArr = mcVersion?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = -1;

        if (versionsArr is { Length: >= 2 }) minor = int.TryParse(versionsArr[1], out var outMinor) ? outMinor : -1;

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
            isNotLinux &&
            minor is -1 or >= 19)
            return libs;

        if (replaceDic == null) return libs;
        if (minor is -1 or >= 19 && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        foreach (var original in libs)
        {
            if (!original.Rules.CheckAllow()) continue;

            var isNative = original.Natives != null;

            if (isNative)
            {
                if (!replaceDic.TryGetValue($"{original.Name}:natives", out var candidateNative) || candidateNative == null)
                {
                    replaced.Add(original);
                    continue;
                }

                replaced.Add(candidateNative);
                continue;
            }

            // Libraries
            if (!replaceDic.TryGetValue(original.Name, out var candidateLib) || candidateLib == null)
            {
                replaced.Add(original);
                continue;
            }

            replaced.Add(candidateLib);
        }

        return replaced;
    }
}