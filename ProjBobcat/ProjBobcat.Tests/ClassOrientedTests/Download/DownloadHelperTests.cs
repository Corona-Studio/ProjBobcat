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
            Assert.IsTrue(handler.RequestedRanges.Count(range => range.End > range.Start) >= 8,
                "An 8 MiB response should use the configured parallelism without creating sub-megabyte parts.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_ProbeLatencyDoesNotReduceTransferParallelism()
    {
        var data = CreateData(16 * 1024 * 1024);
        var handler = new RangeHandler(data) { ProbeDelay = TimeSpan.FromMilliseconds(900) };
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateFile(directory, "delayed-probe.bin", "https://primary.test/delayed-probe.bin", data);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.RequestedRanges.Count(range => range.End > range.Start) >= 8,
                "Connection setup latency must not be treated as low sustained bandwidth.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_AcceptsRangesWithoutOptionalLengthHeaders()
    {
        var data = CreateData(8 * 1024 * 1024);
        var handler = new RangeHandler(data) { OmitTransferLengths = true };
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateFile(directory, "unknown-length.bin", "https://primary.test/unknown-length.bin", data);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler));

            Assert.IsTrue((await completion).Success);
            Assert.AreEqual(0, handler.WholeFileRequests,
                "A valid 206 response must not make the downloader fall back to a full-file request.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_SmallKnownFilesUseTheConfiguredConnectionBudgetWithoutProbes()
    {
        var data = CreateData(32 * 1024);
        var handler = new ConcurrentContentHandler(data, TimeSpan.FromMilliseconds(100));
        var directory = CreateTestDirectory();
        var files = Enumerable.Range(0, 32)
            .Select(index => CreateFile(directory, $"asset-{index}.bin", $"https://assets.test/{index}", data))
            .Cast<AbstractDownloadBase>()
            .ToArray();

        try
        {
            var settings = CreateSettings(handler, downloadThread: 32);

            await DownloadHelper.DownloadAsync(files, settings);

            Assert.AreEqual(0, handler.RangeRequests,
                "Known single-part files should not pay for a separate range probe.");
            Assert.IsTrue(handler.MaximumConcurrency > 16,
                $"Expected the configured connection budget above the old 16-file cap; saw {handler.MaximumConcurrency}.");
            Assert.IsTrue(files.All(file => File.Exists(Path.Combine(directory, file.FileName))));
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
    public async Task DownloadAsync_WholeFileTriesEverySourceBeforeFailing()
    {
        var data = CreateData(256 * 1024);
        var handler = new FailoverHandler(data, "source-5.test");
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateMultiSourceFile(directory, "whole-failover.bin", data, 6);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler, retryCount: 2));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.Hosts.Contains("source-5.test"), "The last configured source was never attempted.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_RangeTransferTriesEverySourceBeforeFallingBack()
    {
        var data = CreateData(8 * 1024 * 1024);
        var handler = new FailoverHandler(data, "source-5.test", "source-0.test");
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateMultiSourceFile(directory, "range-failover.bin", data, 6);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler, retryCount: 2));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.Hosts.Contains("source-5.test"), "The last configured source was never attempted.");
            Assert.AreEqual(0, handler.WholeFileRequests,
                "Range failover should succeed before downgrading to a complete-file transfer.");
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task DownloadAsync_HashMismatchRotatesThroughEverySource()
    {
        var expected = CreateData(256 * 1024);
        var corrupt = expected.ToArray();
        corrupt[0] ^= 0xff;
        var handler = new PerHostContentHandler(new Dictionary<string, byte[]>
        {
            ["source-0.test"] = corrupt,
            ["source-1.test"] = corrupt,
            ["source-2.test"] = expected
        });
        var directory = CreateTestDirectory();

        try
        {
            var file = CreateMultiSourceFile(directory, "hash-failover.bin", expected, 3);
            var completion = CaptureCompletion(file);

            await DownloadHelper.DownloadAsync(file, CreateSettings(handler, checkFile: true, retryCount: 1));

            Assert.IsTrue((await completion).Success);
            Assert.IsTrue(handler.Hosts.Contains("source-2.test"),
                "Hash validation stopped before reaching the valid backup source.");
            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(Path.Combine(directory, file.FileName)));
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

    static MultiSourceDownloadFile CreateMultiSourceFile(
        string directory,
        string name,
        byte[] data,
        int sourceCount)
    {
        return new MultiSourceDownloadFile
        {
            DownloadPath = directory,
            FileName = name,
            FileSize = data.Length,
            CheckSum = Convert.ToHexString(SHA256.HashData(data)),
            DownloadUris = Enumerable.Range(0, sourceCount)
                .Select(index => new DownloadUriInfo($"https://source-{index}.test/{name}", sourceCount - index))
                .ToArray()
        };
    }

    static DownloadSettings CreateSettings(
        HttpMessageHandler handler,
        bool checkFile = false,
        int downloadThread = 16,
        int retryCount = 4)
    {
        return new DownloadSettings
        {
            HttpClientFactory = new TestHttpClientFactory(handler),
            CheckFile = checkFile,
            HashType = HashType.SHA256,
            RetryCount = retryCount,
            DownloadParts = 16,
            DownloadThread = downloadThread,
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
        int _wholeFileRequests;
        (long Start, long End)? _truncatedRange;

        public ConcurrentQueue<(long Start, long End)> RequestedRanges { get; } = new();
        public ConcurrentBag<string> Hosts { get; } = [];
        public bool TruncateFirstTransfer { get; init; }
        public bool OmitTransferLengths { get; init; }
        public TimeSpan ProbeDelay { get; init; }
        public int TransientFailures { get; set; }
        public bool SawResumedRange { get; private set; }
        public int WholeFileRequests => Volatile.Read(ref this._wholeFileRequests);

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
                Interlocked.Increment(ref this._wholeFileRequests);
                var complete = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                complete.Content.Headers.ContentLength = data.Length;
                return Task.FromResult(complete);
            }

            var start = requested.From ?? 0;
            var end = requested.To ?? data.Length - 1;
            this.RequestedRanges.Enqueue((start, end));
            var length = checked((int)(end - start + 1));
            var bytes = data.AsSpan(checked((int)start), length).ToArray();
            HttpContent content = this.OmitTransferLengths && end > start
                ? new UnknownLengthContent(bytes)
                : new ByteArrayContent(bytes);

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

            if (!this.OmitTransferLengths || end == start)
                content.Headers.ContentLength = length;
            content.Headers.ContentRange = this.OmitTransferLengths && end > start
                ? new ContentRangeHeaderValue(start, end)
                : new ContentRangeHeaderValue(start, end, data.Length);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
            return this.ProbeDelay > TimeSpan.Zero && start == 0 && end == 0
                ? DelayResponseAsync(response, this.ProbeDelay, cancellationToken)
                : Task.FromResult(response);
        }

        static async Task<HttpResponseMessage> DelayResponseAsync(
            HttpResponseMessage response,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return response;
        }
    }

    sealed class ConcurrentContentHandler(byte[] data, TimeSpan delay) : HttpMessageHandler
    {
        int _active;
        int _maximumConcurrency;
        int _rangeRequests;

        public int MaximumConcurrency => Volatile.Read(ref this._maximumConcurrency);
        public int RangeRequests => Volatile.Read(ref this._rangeRequests);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Range != null) Interlocked.Increment(ref this._rangeRequests);
            var active = Interlocked.Increment(ref this._active);
            UpdateMaximum(ref this._maximumConcurrency, active);

            try
            {
                await Task.Delay(delay, cancellationToken);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                response.Content.Headers.ContentLength = data.Length;
                return response;
            }
            finally
            {
                Interlocked.Decrement(ref this._active);
            }
        }

        static void UpdateMaximum(ref int maximum, int value)
        {
            var current = Volatile.Read(ref maximum);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref maximum, value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }

    sealed class FailoverHandler(byte[] data, string successfulHost, string? probeHost = null) : HttpMessageHandler
    {
        int _wholeFileRequests;

        public ConcurrentBag<string> Hosts { get; } = [];
        public int WholeFileRequests => Volatile.Read(ref this._wholeFileRequests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            this.Hosts.Add(host);
            var requested = request.Headers.Range?.Ranges.SingleOrDefault();
            if (requested == null) Interlocked.Increment(ref this._wholeFileRequests);

            var isProbe = requested is { From: 0, To: 0 } &&
                          string.Equals(host, probeHost, StringComparison.OrdinalIgnoreCase);
            if (!isProbe && !string.Equals(host, successfulHost, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            if (requested == null)
            {
                var complete = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                complete.Content.Headers.ContentLength = data.Length;
                return Task.FromResult(complete);
            }

            var start = requested.From ?? 0;
            var end = requested.To ?? data.Length - 1;
            var length = checked((int)(end - start + 1));
            var content = new ByteArrayContent(data.AsSpan(checked((int)start), length).ToArray());
            content.Headers.ContentLength = length;
            content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, data.Length);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content });
        }
    }

    sealed class PerHostContentHandler(IReadOnlyDictionary<string, byte[]> contentByHost) : HttpMessageHandler
    {
        public ConcurrentBag<string> Hosts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            this.Hosts.Add(host);
            if (!contentByHost.TryGetValue(host, out var data))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
            response.Content.Headers.ContentLength = data.Length;
            return Task.FromResult(response);
        }
    }

    sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(bytes).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
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
