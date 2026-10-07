using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper.NativeReplace;

public static partial class NativeReplaceHelper
{
    static readonly NativeReplaceModel NativeReplaceModel;

    static NativeReplaceHelper()
    {
        var model = JsonSerializer.Deserialize(ReplaceDicJson, SerializerContext.Default.NativeReplaceModel);

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

        if (javaPlatform == OSPlatform.Linux)
        {
            if (useSystemGlfwOnLinux || useSystemOpenAlOnLinux)
                libs = libs.Where(original =>
                {
                    var maven = original.Name.ResolveMavenString();
                    return maven == null || maven.OrganizationName != "org.lwjgl" ||
                           !maven.Classifier.StartsWith("natives", StringComparison.Ordinal) ||
                           !(useSystemGlfwOnLinux && maven.ArtifactId == "lwjgl-glfw" ||
                             useSystemOpenAlOnLinux && maven.ArtifactId == "lwjgl-openal");
                }).ToList();

            // Modern Mojang manifests still only list Linux x64 natives. Selecting the
            // JVM architecture must also work under LegacyOnly and for unknown game IDs.
            if (javaArch == Architecture.Arm64)
                libs = ReplaceModernLinuxArm64Natives(libs);
        }

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

        var osCheckFlag = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

        if (javaArch.Value == Architecture.X86 && osCheckFlag)
            return libs;

        var isNotLinux = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        var mcVersion = GameVersionHelper.TryGetMcVersion(versions);

        if (string.IsNullOrEmpty(mcVersion) && policy == NativeReplacementPolicy.LegacyOnly) return libs;

        var versionsArr = mcVersion?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = -1;

        // Year-based versions such as 26.3 are not legacy Minecraft 1.x releases.
        if (versionsArr is { Length: >= 2 } && versionsArr[0] == "1")
            minor = int.TryParse(versionsArr[1], out var outMinor) ? outMinor : -1;

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

    static List<Library> ReplaceModernLinuxArm64Natives(List<Library> libs)
    {
        var replaced = new List<Library>(libs.Count);
        foreach (var original in libs)
        {
            // Legacy classifier dictionaries still require the version replacement table.
            if (original.Natives != null || !original.Name.StartsWith("org.lwjgl:", StringComparison.Ordinal))
            {
                replaced.Add(original);
                continue;
            }

            var maven = original.Name.ResolveMavenString();
            // Earlier LWJGL releases use the table to upgrade Java bindings and natives together.
            if (maven == null || maven.Classifier != "natives-linux" ||
                !Version.TryParse(maven.Version, out var version) || version < new Version(3, 3, 2))
            {
                replaced.Add(original);
                continue;
            }

            var name = $"org.lwjgl:{maven.ArtifactId}:{maven.Version}:natives-linux-arm64";
            // Prefer a native already supplied by the manifest, including its checksums.
            if (libs.Any(lib => lib.Name == name)) continue;

            var path = name.ResolveMavenString()!.Path;
            NativeReplaceModel.LinuxArm64.TryGetValue(original.Name, out var candidate);
            var download = candidate?.Name == name ? candidate.Downloads?.Artifact : null;
            replaced.Add(new Library
            {
                Name = name,
                Rules = original.Rules,
                Extract = original.Extract,
                ClientRequired = original.ClientRequired,
                ServerRequired = original.ServerRequired,
                Downloads = new Downloads
                {
                    Artifact = new FileInfo
                    {
                        Name = name,
                        Path = path,
                        Url = $"https://repo1.maven.org/maven2/{path}",
                        // The x64 artifact's SHA1 and size cannot validate the ARM64 JAR.
                        Sha1 = download?.Sha1,
                        Size = download?.Size ?? 0
                    }
                }
            });
        }

        return replaced;
    }
}
