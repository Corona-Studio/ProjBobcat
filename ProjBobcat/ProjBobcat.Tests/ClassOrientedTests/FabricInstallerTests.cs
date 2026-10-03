using System.Text.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.DefaultComponent.Installer;

namespace ProjBobcat.Tests.ClassOrientedTests;

[TestClass]
public class FabricInstallerTests
{
    [TestMethod]
    [DataRow("26.3", "0.0.0", null, "custom-base", "26.3-fabric-0.19.5")]
    [DataRow("1.21.11", "1.21.11", null, "1.21.11", "1.21.11-fabric-0.19.5")]
    [DataRow(null, "0.0.0", null, "26.3", "26.3-fabric-0.19.5")]
    [DataRow(null, "1.21.11", null, null, "1.21.11-fabric-0.19.5")]
    [DataRow("26.3", "0.0.0", "my-fabric", "custom-base", "my-fabric")]
    public async Task WritesActualMinecraftVersionAndPreservesCustomId(
        string? gameVersion, string intermediary, string? customId, string? parent, string expectedId)
    {
        var root = Path.Combine(Path.GetTempPath(), $"fabric-installer-test-{Guid.NewGuid():N}");
        try
        {
            var installer = new FabricInstaller
            {
                RootPath = root,
                CustomId = customId,
                InheritsFrom = parent,
                VersionLocator = null!, // Installation only writes metadata; no services are used.
                HttpClientFactory = null!,
                LoaderArtifact = new FabricLoaderArtifactModel
                {
                    Loader = new FabricArtifactModel
                    {
                        GameVersion = gameVersion, Maven = "net.fabricmc:fabric-loader:0.19.5", Version = "0.19.5"
                    },
                    Intermediary = new FabricArtifactModel
                    {
                        Maven = $"net.fabricmc:intermediary:{intermediary}", Version = intermediary
                    },
                    LauncherMeta = new FabricLauncherMeta
                    {
                        Libraries = new FabricLibraries(),
                        MainClass = JsonSerializer.SerializeToElement(new
                        {
                            client = "net.fabricmc.loader.impl.launch.knot.KnotClient"
                        })
                    }
                }
            };

            Assert.AreEqual(expectedId, await installer.InstallTaskAsync());
            var json = await File.ReadAllTextAsync(GamePathHelper.GetGameJsonPath(root, expectedId));
            var version = JsonSerializer.Deserialize<RawVersionModel>(json)!;
            var expectedMinecraft = gameVersion ?? parent ?? intermediary;
            Assert.AreEqual(expectedMinecraft, version.ClientVersion);
            Assert.AreEqual(parent ?? expectedMinecraft, version.InheritsFrom);
            Assert.AreEqual(expectedMinecraft, GameVersionHelper.TryGetMcVersion([version]));
            Assert.AreEqual($"net.fabricmc:intermediary:{intermediary}", version.Libraries[1].Name);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
