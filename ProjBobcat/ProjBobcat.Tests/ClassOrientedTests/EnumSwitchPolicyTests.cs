using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Services;

namespace ProjBobcat.Tests.ClassOrientedTests;

[TestClass]
public class EnumSwitchPolicyTests
{
    [TestMethod]
    [DataRow(0d, 0d, SizeUnit.B)]
    [DataRow(-1d, -1d, SizeUnit.B)]
    [DataRow(1023d, 1023d, SizeUnit.B)]
    [DataRow(1024d, 1d, SizeUnit.Kb)]
    [DataRow(1048576d, 1d, SizeUnit.Mb)]
    [DataRow(1073741824d, 1d, SizeUnit.Gb)]
    [DataRow(1099511627776d, 1d, SizeUnit.Tb)]
    public void DownloadSpeedKeepsEveryUnitAndBoundary(double bytes, double expected, SizeUnit unit)
    {
        var result = DownloadHelper.AutoFormatSpeed(bytes);

        Assert.AreEqual(expected, result.Speed);
        Assert.AreEqual(unit, result.Unit);
        StringAssert.Contains(DownloadHelper.AutoFormatSpeedString(bytes), unit.ToString());
    }

    [TestMethod]
    [DataRow(GcType.Disable, null)]
    [DataRow(GcType.G1Gc, "-XX:+UseG1GC")]
    [DataRow(GcType.SerialGc, "-XX:+UseSerialGC")]
    [DataRow(GcType.ParallelGc, "-XX:+UseParallelGC")]
    [DataRow(GcType.CmsGc, "-XX:+UseConcMarkSweepGC")]
    [DataRow(GcType.ZGc, "-XX:+UseZGC")]
    [DataRow((GcType)(-1), "-XX:+UseG1GC")]
    [DataRow((GcType)999, "-XX:+UseG1GC")]
    public void GarbageCollectorOptionsPreserveDisabledAndUnknownSettings(GcType gcType, string? expected)
    {
        var settings = new LaunchSettings
        {
            GameName = "test",
            GamePath = "test",
            GameResourcePath = "test",
            Version = "test",
            UseShellExecute = false,
            NativeReplacementPolicy = NativeReplacementPolicy.Disabled,
            VersionLocator = null!,
            Authenticator = null!,
            GameArguments = new GameArguments { Java = null, MaxMemory = 1024, GcType = gcType },
            GameEnvironmentVariables = [],
            UseV4NetworkingStack = false,
            PreferUtf8Encoding = true,
            AutoApplyFixForOldMultiPlayerGame = false,
            OldGameTrustStorePath = null
        };
        var parser = new DefaultLaunchArgumentParser(null!, string.Empty);

        var arguments = parser.ParseJvmHeadArguments(settings).ToArray();

        Assert.AreEqual(expected, arguments.SingleOrDefault(argument => argument.StartsWith("-XX:+Use")));
        CollectionAssert.Contains(arguments, "-Xmx1024m");
    }

    [TestMethod]
    [DataRow(HashType.MD5)]
    [DataRow(HashType.SHA256)]
    [DataRow(HashType.SHA384)]
    [DataRow((HashType)(-1))]
    [DataRow((HashType)999)]
    public async Task ModrinthRejectsUnsupportedAndUndefinedHashAlgorithmsBeforeSending(HashType hashType)
    {
        using var client = new HttpClient(new NoRequestsHandler());
        var service = new ModrinthApiService(client, null!);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.TryMatchVersionFileByHash("test-hash", hashType));
    }

    private sealed class NoRequestsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new AssertFailedException("Unsupported hashes must not send requests.");
    }
}
