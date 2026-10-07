using System.Runtime.InteropServices;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.NativeReplace;
using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using BobcatFileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.Tests.ClassOrientedTests;

[TestClass]
public class NativeReplaceHelperTests
{
    static Library Library(string name) => new()
    {
        Name = name,
        Downloads = new Downloads
        {
            Artifact = new BobcatFileInfo
            {
                Path = name.ResolveMavenString()!.Path,
                Url = "https://libraries.minecraft.net/" + name.ResolveMavenString()!.Path,
                Sha1 = "x64-only-checksum",
                Size = 123
            }
        }
    };

    static List<Library> Replace(List<Library> libs, string gameVersion = "26.3",
        NativeReplacementPolicy policy = NativeReplacementPolicy.LegacyOnly,
        Architecture architecture = Architecture.Arm64, bool glfw = false, bool openal = false) =>
        NativeReplaceHelper.Replace(
            [new() { Id = gameVersion, MainClass = "net.minecraft.client.main.Main", Libraries = [.. libs] }],
            libs, policy, OSPlatform.Linux, architecture, glfw, openal);

    [TestMethod]
    [DataRow("1.21.11", "3.3.3", NativeReplacementPolicy.LegacyOnly)]
    [DataRow("1.21.11", "3.3.3", NativeReplacementPolicy.All)]
    [DataRow("26.3", "3.4.3", NativeReplacementPolicy.LegacyOnly)]
    [DataRow("26.3", "3.4.3", NativeReplacementPolicy.All)]
    [DataRow("custom-instance", "3.4.3", NativeReplacementPolicy.LegacyOnly)]
    [DataRow("26.3-pre-1", "3.4.3", NativeReplacementPolicy.LegacyOnly)]
    public void ModernNativesMatchJavaArchitectureWithoutChangingLwjglVersion(
        string gameVersion, string lwjglVersion, NativeReplacementPolicy policy)
    {
        string[] modules = lwjglVersion == "3.3.3"
            ? ["lwjgl", "lwjgl-freetype", "lwjgl-jemalloc", "lwjgl-openal", "lwjgl-opengl",
                "lwjgl-glfw", "lwjgl-stb", "lwjgl-tinyfd"]
            : ["lwjgl", "lwjgl-freetype", "lwjgl-jemalloc", "lwjgl-openal", "lwjgl-opengl",
                "lwjgl-stb", "lwjgl-sdl", "lwjgl-shaderc", "lwjgl-spvc", "lwjgl-vma"];
        var libs = modules.SelectMany(module => new[]
        {
            Library($"org.lwjgl:{module}:{lwjglVersion}"),
            Library($"org.lwjgl:{module}:{lwjglVersion}:natives-linux")
        }).ToList();
        var result = Replace(libs, gameVersion, policy);
        Assert.HasCount(libs.Count, result);

        foreach (var module in modules)
        {
            var coreName = $"org.lwjgl:{module}:{lwjglVersion}";
            Assert.AreSame(libs.Single(lib => lib.Name == coreName), result.Single(lib => lib.Name == coreName));
            var native = result.Single(lib => lib.Name == coreName + ":natives-linux-arm64");
            var artifact = native.Downloads!.Artifact!;
            Assert.AreEqual(native.Name.ResolveMavenString()!.Path, artifact.Path);
            Assert.AreEqual("https://repo1.maven.org/maven2/" + artifact.Path, artifact.Url);
            Assert.AreNotEqual("x64-only-checksum", artifact.Sha1);
            Assert.AreNotEqual(123L, artifact.Size);
            Assert.AreEqual(LibraryType.ReplacementNative, LibraryInfoResolver.GetLibType(artifact));
        }

        // Native JARs must remain on the classpath for LWJGL's runtime loader.
        var (natives, classpath) = new DefaultVersionLocator(Path.GetTempPath()).GetNatives([.. result]);
        Assert.HasCount(modules.Length, natives);
        Assert.HasCount(modules.Length * 2, classpath);
        Assert.IsFalse(classpath.Any(file => file.Name!.EndsWith(":natives-linux", StringComparison.Ordinal)));
        Assert.IsTrue(natives.All(native => classpath.Contains(native.FileInfo)));
        Assert.IsTrue(libs.Any(lib => lib.Name.EndsWith(":natives-linux", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DisabledAndX64KeepOriginalLibraries()
    {
        List<Library> libs = [Library("org.lwjgl:lwjgl:3.4.3:natives-linux")];
        Assert.AreSame(libs, Replace(libs, policy: NativeReplacementPolicy.Disabled));
        Assert.AreSame(libs, Replace(libs, architecture: Architecture.X64));
    }

    [TestMethod]
    public void KeepsExistingArm64NativeAndOtherPlatforms()
    {
        var arm64 = Library("org.lwjgl:lwjgl:3.4.3:natives-linux-arm64");
        var macos = Library("org.lwjgl:lwjgl:3.4.3:natives-macos");
        var unrelated = Library("com.example:library:1.0:natives-linux");
        var result = Replace([Library("org.lwjgl:lwjgl:3.4.3:natives-linux"), arm64, macos, unrelated]);
        CollectionAssert.AreEqual(new[] { arm64, macos, unrelated }, result);
    }

    [TestMethod]
    public void PreservesRulesAndExtractionWithoutReusingX64Metadata()
    {
        var original = Library("org.lwjgl:lwjgl-sdl:3.4.3:natives-linux");
        original.Rules = [new() { Action = "allow", OperatingSystem = new() { Name = "linux" } }];
        original.Extract = new Extract { Exclude = ["META-INF/"] };
        var native = Replace([original]).Single();
        Assert.AreSame(original.Rules, native.Rules);
        Assert.AreSame(original.Extract, native.Extract);
        Assert.IsNull(native.Downloads!.Artifact!.Sha1);
        Assert.AreEqual(0L, native.Downloads.Artifact.Size);
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void SystemLibrariesRemoveOnlyRequestedNatives(bool glfw, bool openal)
    {
        List<Library> libs =
        [
            Library("org.lwjgl:lwjgl:3.4.3:natives-linux"),
            Library("org.lwjgl:lwjgl-glfw:3.4.3"),
            Library("org.lwjgl:lwjgl-glfw:3.4.3:natives-linux"),
            Library("org.lwjgl:lwjgl-openal:3.4.3"),
            Library("org.lwjgl:lwjgl-openal:3.4.3:natives-linux")
        ];
        var result = Replace(libs, glfw: glfw, openal: openal);
        Assert.HasCount(5 - (glfw ? 1 : 0) - (openal ? 1 : 0), result);
        Assert.IsTrue(result.Any(lib => lib.Name == "org.lwjgl:lwjgl:3.4.3:natives-linux-arm64"));
        Assert.AreEqual(!glfw, result.Any(lib => lib.Name == "org.lwjgl:lwjgl-glfw:3.4.3:natives-linux-arm64"));
        Assert.AreEqual(!openal, result.Any(lib => lib.Name == "org.lwjgl:lwjgl-openal:3.4.3:natives-linux-arm64"));
    }

    [TestMethod]
    public void LegacyLwjglStillUsesVersionReplacementTable()
    {
        var original = Library("org.lwjgl:lwjgl:3.1.6");
        var native = new Library
        {
            Name = original.Name,
            Natives = new Dictionary<string, string> { ["linux"] = "natives-linux" }
        };
        var result = Replace([original, native], "1.12.2");
        CollectionAssert.AreEqual(new[] { "org.lwjgl:lwjgl:3.3.2", "org.lwjgl:lwjgl:3.3.2:natives-linux-arm64" },
            result.Select(lib => lib.Name).ToArray());
    }

    [TestMethod]
    public void ModLoaderInheritedLibrariesAlsoReplaceNatives()
    {
        var parent = new RawVersionModel
        {
            Id = "26.3", MainClass = "net.minecraft.client.main.Main",
            Libraries = [Library("org.lwjgl:lwjgl:3.4.3:natives-linux")]
        };
        var child = new RawVersionModel
        {
            Id = "26.3-fabric", InheritsFrom = parent.Id, MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient",
            Libraries = [Library("org.lwjgl:lwjgl-sdl:3.4.3:natives-linux")]
        };
        var info = new VersionInfo
        {
            Id = child.Id, Name = child.Id, DirName = child.Id, GameBaseVersion = parent.Id,
            InheritsFrom = parent.Id, RawVersion = child, InheritsVersions = [parent]
        };
        var result = new DefaultVersionLocator(Path.GetTempPath()).ResolveGame(info,
            NativeReplacementPolicy.LegacyOnly, new JavaRuntimeInfo("java", OSPlatform.Linux, Architecture.Arm64, false, false));
        Assert.IsNotNull(result);
        CollectionAssert.AreEquivalent(new[]
        {
            "org.lwjgl:lwjgl:3.4.3:natives-linux-arm64", "org.lwjgl:lwjgl-sdl:3.4.3:natives-linux-arm64"
        }, result.Libraries.Select(lib => lib.Name).ToArray());
    }
}
