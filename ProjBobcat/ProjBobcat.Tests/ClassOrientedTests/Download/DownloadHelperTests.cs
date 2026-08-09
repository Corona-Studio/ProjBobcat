using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Event;

namespace ProjBobcat.Tests.ClassOrientedTests.Download;

[TestClass]
public sealed class DownloadHelperTests
{
    [TestMethod]
    public async Task DownloadAsync_AutomaticallyUsesRangesAndVerifiesFile()
    {
        var data = CreateData(8 * 1024 * 1024);
        var handler = new RangeHandler(data);
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateFile(directory, "range.bin", "https://primary.test/range.bin", data);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler, checkFile: true));

            var result = await completion;
            Assert.IsTrue(result.Success, result.Error?.ToString());
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
            Assert.IsTrue(handler.RequestedRanges.Count(range => range.End > range.Start) >= 2,
                "An 8 MiB response should be split automatically when the server supports ranges.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_SwitchesToTheNextMirrorImmediately()
    {
        var data = CreateData(2 * 1024 * 1024);
        var handler = new RangeHandler(data, "dead.test");
        var directory = CreateTestDirectory();

        try
        {
            var file = new MultiSourceDownloadFile
            {
                DownloadPath = directory,
                FileName = "mirror.bin",
                FileSize = data.Length,
                DownloadUris =
                [
                    new DownloadUriInfo("https://dead.test/mirror.bin", 10),
                    new DownloadUriInfo("https://healthy.test/mirror.bin", 1)
                ]
            };
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.Hosts.Contains("dead.test"));
            Assert.IsTrue(handler.Hosts.Contains("healthy.test"));
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_RetriesATransientProbeFailure()
    {
        var data = CreateData(1024 * 1024);
        var handler = new RangeHandler(data) { TransientFailures = 1 };
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateFile(directory, "retry.bin", "https://primary.test/retry.bin", data);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(file.RetryCount >= 1);
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_ResumesOnlyTheMissingPartOfATruncatedRange()
    {
        var data = CreateData(8 * 1024 * 1024);
        var handler = new RangeHandler(data) { TruncateFirstTransfer = true };
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateFile(directory, "resume.bin", "https://primary.test/resume.bin", data);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.SawResumedRange,
                "A truncated segment should resume at its last written byte instead of restarting the file.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_ListProgressIsMonotonicAndCompletesOncePerFile()
    {
        var data = CreateData(2 * 1024 * 1024);
        var handler = new RangeHandler(data);
        var directory = CreateTestDirectory();

        try
        {
            var files = new AbstractDownloadBase[]
            {
                CreateFile(directory, "first.bin", "https://primary.test/first.bin", data),
                CreateFile(directory, "second.bin", "https://primary.test/second.bin", data)
            };
            var progress = new ConcurrentQueue<double>();
            var completionCount = 0;
            foreach (var file in files)
            {
                file.Changed += (_, args) => progress.Enqueue(args.ProgressPercentage.NormalizedValue);
                file.Completed += (_, _) => Interlocked.Increment(ref completionCount);
            }

            await DownloadHelper.DownloadAsync(files, CreateSettings(handler));

            Assert.AreEqual(files.Length, completionCount);
            var snapshots = progress.ToArray();
            Assert.IsTrue(snapshots.Length > 0);
            Assert.AreEqual(1d, snapshots[^1], 0.0001);
            for (var i = 1; i < snapshots.Length; i++)
                Assert.IsTrue(snapshots[i] >= snapshots[i - 1],
                    $"Progress regressed from {snapshots[i - 1]} to {snapshots[i]}.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    static SimpleDownloadFile CreateFile(string directory, string name, string url, byte[] data)
    {
        return new SimpleDownloadFile
        {
            DownloadPath = directory,
            FileName = name,
            DownloadUri = url,
            FileSize = data.Length,
            CheckSum = Convert.ToHexString(SHA256.HashData(data))
        };
    }

    static DownloadSettings CreateSettings(HttpMessageHandler handler, bool checkFile = false)
    {
        return new DownloadSettings
        {
            HttpClientFactory = new TestHttpClientFactory(handler),
            CheckFile = checkFile,
            HashType = HashType.SHA256,
            RetryCount = 4,
            DownloadParts = 16,
            DownloadThread = 16,
            ConnectionTimeout = TimeSpan.FromSeconds(1),
            StallTimeout = TimeSpan.FromSeconds(1),
            ProgressInterval = TimeSpan.FromMilliseconds(50)
        };
    }

    static Task<DownloadFileCompletedEventArgs> CaptureCompletion(AbstractDownloadBase file)
    {
        var completion = new TaskCompletionSource<DownloadFileCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        file.Completed += (_, args) => completion.TrySetResult(args);
        return completion.Task;
    }

    static byte[] CreateData(int length)
    {
        var data = new byte[length];
        new Random(42).NextBytes(data);
        return data;
    }

    static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ProjBobcat.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, false);
        }
    }

    sealed class RangeHandler(byte[] data, string? failingHost = null) : HttpMessageHandler
    {
        readonly object _sync = new();
        bool _truncated;
        (long Start, long End)? _truncatedRange;

        public ConcurrentQueue<(long Start, long End)> RequestedRanges { get; } = new();
        public ConcurrentBag<string> Hosts { get; } = [];
        public bool TruncateFirstTransfer { get; init; }
        public int TransientFailures { get; set; }
        public bool SawResumedRange { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            this.Hosts.Add(host);
            if (string.Equals(host, failingHost, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            lock (this._sync)
            {
                if (this.TransientFailures > 0)
                {
                    this.TransientFailures--;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }
            }

            var requested = request.Headers.Range?.Ranges.SingleOrDefault();
            if (requested == null)
            {
                var complete = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                complete.Content.Headers.ContentLength = data.Length;
                return Task.FromResult(complete);
            }

            var start = requested.From ?? 0;
            var end = requested.To ?? data.Length - 1;
            this.RequestedRanges.Enqueue((start, end));
            var length = checked((int)(end - start + 1));
            var bytes = data.AsSpan(checked((int)start), length).ToArray();
            HttpContent content = new ByteArrayContent(bytes);

            lock (this._sync)
            {
                if (this.TruncateFirstTransfer && !this._truncated && end > start)
                {
                    this._truncated = true;
                    this._truncatedRange = (start, end);
                    content = new ShortContent(bytes[..Math.Max(1, bytes.Length / 2)], length);
                }
                else if (this._truncatedRange is { } truncated && start > truncated.Start && end == truncated.End)
                {
                    this.SawResumedRange = true;
                }
            }

            content.Headers.ContentLength = length;
            content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, data.Length);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content });
        }
    }

    sealed class ShortContent(byte[] bytes, long advertisedLength) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(bytes).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = advertisedLength;
            return true;
        }
    }
}
