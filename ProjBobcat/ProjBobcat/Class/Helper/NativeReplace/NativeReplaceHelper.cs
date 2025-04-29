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

    static string GetNativeKey(OSPlatform platform, Architecture architecture)
    {
        var platformStr = platform switch
        {
            _ when platform == OSPlatform.Windows => "windows",
            _ when platform == OSPlatform.Linux => "linux",
            _ when platform == OSPlatform.OSX => "osx",
            _ when platform == OSPlatform.FreeBSD => "freebsd",
            _ => string.Empty
        };

        var archStr = architecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm32",
            Architecture.LoongArch64 => "loongarch64",
            _ => string.Empty
        };

        return $"{platformStr}-{archStr}";
    }

    public static List<Library> Replace(
        List<RawVersionModel> versions,
        List<Library> libs,
        NativeReplacementPolicy policy,
        OSPlatform? javaPlatform,
        Architecture? javaArch,
        bool useSystemGlfwOnLinux,
        bool useSystemOpenAlOnLinux)
    {
        if (policy == NativeReplacementPolicy.Disabled) return libs;

        javaPlatform ??= SystemInfoHelper.GetOsPlatform();
        javaArch ??= RuntimeInformation.OSArchitecture;

        var replaceKey = GetNativeKey(javaPlatform.Value, javaArch.Value);
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
                        replaced.Add(original);
                }
            }

        var osCheckFlag = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

        if (javaArch.Value == Architecture.X86 && osCheckFlag)
            return libs;

        var isNotLinux = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        var mcVersion = GameVersionHelper.TryGetMcVersion(versions);

        if (string.IsNullOrEmpty(mcVersion) && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        var versionsArr = mcVersion?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = -1;

        if (versionsArr is { Length: >= 2 }) minor = int.TryParse(versionsArr[1], out var outMinor) ? outMinor : -1;

        if (javaArch.Value == Architecture.Arm64 &&
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
                if (!replaceDic.TryGetValue($"{original.Name}:natives", out var candidateNative) ||
                    candidateNative == null)
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