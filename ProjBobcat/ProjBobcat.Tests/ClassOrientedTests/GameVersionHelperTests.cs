using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Tests.ClassOrientedTests;

[TestClass]
public class GameVersionHelperTests
{
    private static RawVersionModel FabricVersion(string? parent, string? clientVersion = null) => new()
    {
        Id = "0.0.0-fabric-0.19.5",
        MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient",
        InheritsFrom = parent,
        ClientVersion = clientVersion,
        Libraries =
        [
            new() { Name = "net.fabricmc:intermediary:0.0.0" },
            new() { Name = "net.fabricmc:fabric-loader:0.19.5" }
        ]
    };

    [TestMethod]
    [DataRow("1.21.11")]
    [DataRow("26.3")]
    [DataRow("26.1.2")]
    [DataRow("26.1-snapshot-4")]
    [DataRow("26.3-pre-1")]
    [DataRow("1.21.11-rc1")]
    [DataRow("25w45a")]
    public void PlaceholderIntermediaryFallsBackToParent(string minecraftVersion)
    {
        var version = FabricVersion(minecraftVersion);
        Assert.AreEqual(minecraftVersion, GameVersionHelper.TryGetMcVersion([version]));
        Assert.AreEqual("0.19.5", GameVersionHelper.TryGetFabricVersion(version));
    }

    [TestMethod]
    public void ClientVersionWorksWithCustomParentId()
    {
        Assert.AreEqual("26.3", GameVersionHelper.TryGetMcVersion([FabricVersion("my-base", "26.3")]));
    }

    [TestMethod]
    public void ExistingInstanceWithCustomParentResolvesThroughInheritance()
    {
        var parent = new RawVersionModel
        {
            Id = "my-base", ClientVersion = "26.3", MainClass = "net.minecraft.client.main.Main", Libraries = []
        };
        Assert.AreEqual("26.3", GameVersionHelper.TryGetMcVersion([FabricVersion(parent.Id), parent]));
    }

    [TestMethod]
    public void LegacyIntermediaryStillIdentifiesMinecraft()
    {
        var version = FabricVersion("custom-parent");
        version.Libraries[0].Name = "net.fabricmc:intermediary:1.21.11";
        Assert.AreEqual("1.21.11", GameVersionHelper.TryGetMcVersion([version]));
    }

    [TestMethod]
    [DataRow("0.0.0")]
    [DataRow("my-1.21.11-instance")]
    [DataRow("26.3-fabric-0.19.5")]
    public void DoesNotExportPlaceholderOrInstanceIdAsMinecraftVersion(string parent)
    {
        Assert.IsNull(GameVersionHelper.TryGetMcVersion([FabricVersion(parent)]));
    }
}
