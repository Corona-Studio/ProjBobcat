using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using System.Diagnostics;

namespace ProjBobcat.Tests.ClassOrientedTests;

[TestClass]
public sealed class ProfileIndependentLaunchTests
{
    [TestMethod]
    public void ExplicitEnvironmentOverridesPreserveEmptyAndWhitespaceValues()
    {
        var process = new ProcessStartInfo();
        process.EnvironmentVariables["LX_TEST_EMPTY"] = "inherited";
        DefaultGameCore.ApplyEnvironmentVariables(process,
            ["LX_TEST_EMPTY=", "LX_TEST_SPACES= value ", "LX_TEST_QUOTES=\"literal\"", "LX_TEST_EQUALS=a=b"]);
        Assert.AreEqual("", process.EnvironmentVariables["LX_TEST_EMPTY"]);
        Assert.AreEqual(" value ", process.EnvironmentVariables["LX_TEST_SPACES"]);
        Assert.AreEqual("\"literal\"", process.EnvironmentVariables["LX_TEST_QUOTES"]);
        Assert.AreEqual("a=b", process.EnvironmentVariables["LX_TEST_EQUALS"]);
    }

    [TestMethod]
    public void VersionDiscoveryDoesNotCreateOrRewriteLauncherProfiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bobcat-profile-free-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "versions", "test"));
        var profilePath = Path.Combine(root, "launcher_profiles.json");
        const string existing = "{\"profiles\":{\"legacy\":{\"name\":\"Do not rewrite\"}}}";
        try
        {
            File.WriteAllText(Path.Combine(root, "versions", "test", "test.json"),
                "{\"id\":\"test\",\"type\":\"release\",\"mainClass\":\"net.minecraft.client.main.Main\",\"libraries\":[]}");
            var locator = new DefaultVersionLocator(root);
            locator.GetGame("test");
            Assert.IsFalse(File.Exists(profilePath));
            Assert.IsFalse(File.Exists(Path.Combine(root, "launcher_accounts.json")));
            File.WriteAllText(profilePath, existing);
            locator.GetGame("test");
            Assert.AreEqual(existing, File.ReadAllText(profilePath));
        }
        finally { Directory.Delete(root, true); }
    }
}
