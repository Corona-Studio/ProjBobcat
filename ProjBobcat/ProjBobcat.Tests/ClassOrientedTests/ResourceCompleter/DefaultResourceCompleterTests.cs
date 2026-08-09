using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.DefaultComponent;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.Tests.ClassOrientedTests.ResourceCompleter;

[TestClass]
public sealed class DefaultResourceCompleterTests
{
    [TestMethod]
    public async Task CheckAndDownloadAsync_FixesADeduplicatedListWithAStableTotal()
    {
        var data = new byte[16 * 1024];
        new Random(42).NextBytes(data);
        var directory = CreateTestDirectory();
        var first = CreateResource(directory, "first.jar", data);
        var duplicate = CreateResource(directory, "first.jar", data);
        var second = CreateResource(directory, "second.jar", data);
        var totals = new ConcurrentBag<ulong>();
        var progress = new ConcurrentQueue<double>();
        var completed = 0;

        try
        {
            using var completer = new DefaultResourceCompleter
            {
                HttpClientFactory = new TestHttpClientFactory(new ContentHandler(data)),
                ResourceInfoResolvers =
                [
                    new TestResolver([first, second]),
                    new TestResolver([duplicate])
                ],
                MaxDegreeOfParallelism = 2,
                RandomizeDownloadOrder = false,
                CheckFile = true
            };
            completer.DownloadFileChangedEvent += (_, args) =>
                progress.Enqueue(args.ProgressPercentage.NormalizedValue);
            completer.DownloadFileCompletedEvent += (_, args) =>
            {
                totals.Add(args.TotalNeedToDownload);
                Interlocked.Increment(ref completed);
            };

            var result = await completer.CheckAndDownloadTaskAsync(
                directory,
                true,
                CreateResolvedGame(),
                TestContext.CancellationToken);

            Assert.AreEqual(TaskResultStatus.Success, result.TaskStatus, result.Message);
            Assert.AreEqual(2, completed);
            Assert.IsTrue(totals.All(total => total == 2), "The total must be fixed before downloads begin.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, "first.jar")));
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, "second.jar")));

            var snapshots = progress.ToArray();
            Assert.IsTrue(snapshots.Length > 0);
            Assert.AreEqual(1d, snapshots[^1], 0.0001);
            for (var i = 1; i < snapshots.Length; i++)
                Assert.IsTrue(snapshots[i] >= snapshots[i - 1]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task CheckAndDownloadAsync_TimeoutCancelsAndJoinsResolverBeforeReturning()
    {
        var resolver = new BlockingResolver();
        using var completer = new DefaultResourceCompleter
        {
            HttpClientFactory = new TestHttpClientFactory(new ContentHandler([])),
            ResourceInfoResolvers = [resolver],
            ResolverTimeout = TimeSpan.FromMilliseconds(50)
        };

        var result = await completer.CheckAndDownloadTaskAsync(
            Path.GetTempPath(),
            true,
            CreateResolvedGame(),
            TestContext.CancellationToken);

        Assert.AreEqual(TaskResultStatus.Error, result.TaskStatus);
        Assert.IsTrue(resolver.HasStopped, "The timed-out resolver must not continue running after return.");
    }

    [TestMethod]
    public async Task AssetResolver_CorruptIndexIsReplacedAndCheckedInTheSameRun()
    {
        var directory = CreateTestDirectory();
        var indexDirectory = Path.Combine(directory, GamePathHelper.GetAssetsRoot(), "indexes");
        var indexPath = Path.Combine(indexDirectory, "test.json");
        Directory.CreateDirectory(indexDirectory);
        await File.WriteAllTextAsync(indexPath, "not-json", TestContext.CancellationToken);

        try
        {
            var resolver = new AssetInfoResolver
            {
                HttpClientFactory = new TestHttpClientFactory(
                    new ContentHandler("{\"objects\":{}}"u8.ToArray())),
                Versions =
                [
                    new VersionManifestVersionsModel
                    {
                        Id = "test",
                        Type = "release",
                        Url = "https://resources.test/version.json"
                    }
                ]
            };
            var resolvedGame = CreateResolvedGame() with
            {
                AssetInfo = new Asset
                {
                    Id = "test",
                    Url = "https://resources.test/test.json"
                }
            };
            var resources = new List<IGameResource>();

            await foreach (var resource in resolver.ResolveResourceAsync(
                               directory,
                               true,
                               resolvedGame,
                               TestContext.CancellationToken))
                resources.Add(resource);

            Assert.AreEqual(0, resources.Count);
            Assert.AreEqual("{\"objects\":{}}", await File.ReadAllTextAsync(indexPath, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    public TestContext TestContext { get; set; }

    static TestResource CreateResource(string directory, string fileName, byte[] data)
    {
        return new TestResource
        {
            Path = directory,
            FileName = fileName,
            Title = fileName,
            Type = ResourceType.LibraryOrNative,
            Urls = [new DownloadUriInfo($"https://resources.test/{fileName}", 1)],
            FileSize = data.Length,
            CheckSum = Convert.ToHexString(SHA1.HashData(data))
        };
    }

    static ResolvedGameVersion CreateResolvedGame()
    {
        return new ResolvedGameVersion(
            null,
            "test",
            "test.Main",
            null,
            null,
            null,
            [],
            [],
            null,
            null,
            null);
    }

    static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ProjBobcat.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    sealed class TestResolver(IReadOnlyList<IGameResource> resources) : IResourceInfoResolver
    {
        public event EventHandler<GameResourceInfoResolveEventArgs>? GameResourceInfoResolveEvent
        {
            add { }
            remove { }
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
            string basePath,
            bool checkLocalFiles,
            ResolvedGameVersion resolvedGame,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var resource in resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return resource;
            }
        }

        public IEnumerable<IGameResource> ResolveResource(
            string basePath,
            bool checkLocalFiles,
            ResolvedGameVersion resolvedGame) => resources;

        public void Dispose()
        {
        }
    }

    sealed class BlockingResolver : IResourceInfoResolver
    {
        public bool HasStopped { get; private set; }
        public event EventHandler<GameResourceInfoResolveEventArgs>? GameResourceInfoResolveEvent
        {
            add { }
            remove { }
        }

        public async IAsyncEnumerable<IGameResource> ResolveResourceAsync(
            string basePath,
            bool checkLocalFiles,
            ResolvedGameVersion resolvedGame,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                this.HasStopped = true;
            }

            yield break;
        }

        public IEnumerable<IGameResource> ResolveResource(
            string basePath,
            bool checkLocalFiles,
            ResolvedGameVersion resolvedGame) => [];

        public void Dispose()
        {
        }
    }

    sealed class TestResource : IGameResource
    {
        public required string Path { get; init; }
        public required string Title { get; init; }
        public required ResourceType Type { get; init; }
        public required IReadOnlyList<DownloadUriInfo> Urls { get; init; }
        public required string FileName { get; init; }
        public long FileSize { get; init; }
        public string? CheckSum { get; init; }
    }

    sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    sealed class ContentHandler(byte[] data) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data)
            };
            response.Content.Headers.ContentLength = data.Length;
            return Task.FromResult(response);
        }
    }
}
