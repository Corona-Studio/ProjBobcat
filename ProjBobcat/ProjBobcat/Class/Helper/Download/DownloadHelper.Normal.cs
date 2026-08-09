using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Helper.Download;

public static partial class DownloadHelper
{
    const int CopyBufferSize = 128 * 1024;
    const int DefaultMaxParts = 16;
    const int MaxConcurrentConnections = 64;
    const int MaxConcurrentFileDownloads = 16;

    static async Task DownloadFileCoreAsync(
        AbstractDownloadBase downloadFile,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        Action<AbstractDownloadBase, double, long, long, bool>? aggregateProgress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(GetTempDownloadPath());
        Directory.CreateDirectory(downloadFile.DownloadPath);
        downloadFile.ResetDownloadState();

        var outputPath = Path.Combine(downloadFile.DownloadPath, downloadFile.FileName);
        var tempPath = Path.Combine(
            downloadFile.DownloadPath,
            $".{downloadFile.FileName}.{Guid.NewGuid():N}.download");
        DownloadProgressReporter? progress = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sources = GetDownloadSources(downloadFile);
            var capabilities = await ProbeSourcesAsync(downloadFile, sources, downloadFile.FileSize, settings,
                    connectionGate, cancellationToken)
                .ConfigureAwait(false);
            sources = [capabilities.Url, .. sources.Where(source => !SourceEquals(source, capabilities.Url))];

            progress = new DownloadProgressReporter(
                capabilities.FileLength,
                settings.ProgressInterval,
                (speed, bytes, total, finished) =>
                {
                    if (aggregateProgress != null)
                        aggregateProgress(downloadFile, speed, bytes, total, finished);
                    else
                        downloadFile.OnChanged(speed,
                            finished
                                ? ProgressValue.Finished
                                : total > 0
                                    ? ProgressValue.Create(bytes, total)
                                    : ProgressValue.Start,
                            bytes,
                            total);
                });

            var validationAttempts = settings.CheckFile && !string.IsNullOrWhiteSpace(downloadFile.CheckSum)
                ? Math.Min(2, GetMaxAttempts(settings))
                : 1;
            Exception? validationError = null;

            for (var validationAttempt = 0; validationAttempt < validationAttempts; validationAttempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Reset();

                if (capabilities.SupportsRanges && capabilities.FileLength > 0)
                {
                    var partCount = CalculateAdaptivePartCount(
                        capabilities.FileLength,
                        capabilities.ResponseTime,
                        settings);
                    try
                    {
                        await DownloadRangesAsync(downloadFile, tempPath, capabilities.FileLength, partCount, sources,
                                validationAttempt, settings, connectionGate, progress, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Some CDNs accept bytes=0-0 but reject larger ranges. Downgrade automatically instead of
                        // leaving the download stuck in a multipart retry loop.
                        progress.Reset();
                        await DownloadWholeFileAsync(downloadFile, tempPath, sources, capabilities.FileLength,
                                validationAttempt, settings, connectionGate, progress, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await DownloadWholeFileAsync(downloadFile, tempPath, sources, capabilities.FileLength, validationAttempt,
                            settings, connectionGate, progress, cancellationToken)
                        .ConfigureAwait(false);
                }

                try
                {
                    await VerifyFileAsync(tempPath, downloadFile, settings, cancellationToken).ConfigureAwait(false);
                    validationError = null;
                    break;
                }
                catch (InvalidDataException ex) when (validationAttempt + 1 < validationAttempts)
                {
                    validationError = ex;
                    downloadFile.IncrementRetryCount();
                    await Task.Delay(CalculateRetryDelay(validationAttempt + 1), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (validationError != null) throw validationError;

            File.Move(tempPath, outputPath, true);
            var averageSpeed = await progress.CompleteAsync(true).ConfigureAwait(false);
            downloadFile.OnCompleted(true, null, averageSpeed);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            var averageSpeed = progress == null ? 0 : await progress.CompleteAsync(false).ConfigureAwait(false);
            TryDeleteFile(tempPath);
            downloadFile.OnCompleted(false, ex, averageSpeed);
            throw;
        }
        catch (Exception ex)
        {
            // Completion subscribers historically use exceptions to fail their owning task. Do not translate a
            // subscriber exception into a second, contradictory completion notification.
            if (downloadFile.CompletionRaised) throw;
            var averageSpeed = progress == null ? 0 : await progress.CompleteAsync(false).ConfigureAwait(false);
            TryDeleteFile(tempPath);
            downloadFile.OnCompleted(false, ex, averageSpeed);
        }
        finally
        {
            if (progress != null)
                await progress.DisposeAsync().ConfigureAwait(false);
        }
    }

    static string[] GetDownloadSources(AbstractDownloadBase downloadFile)
    {
        var sources = downloadFile switch
        {
            MultiSourceDownloadFile multiSource => multiSource.DownloadUris
                .Where(item => !string.IsNullOrWhiteSpace(item.DownloadUri) && item.Weight > 0)
                .OrderByDescending(item => item.Weight)
                .Select(item => item.DownloadUri),
            SimpleDownloadFile simple => [simple.DownloadUri],
            _ => [downloadFile.GetDownloadUrl()]
        };

        var result = sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (result.Length == 0)
            throw new InvalidOperationException("No valid download URL was provided.");
        return result;
    }

    static bool SourceEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static async Task<ServerCapabilities> ProbeSourcesAsync(
        AbstractDownloadBase downloadFile,
        IReadOnlyList<string> sources,
        long declaredFileSize,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();

        var attempts = Math.Max(sources.Count, GetMaxAttempts(settings));
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var source = sources[attempt % sources.Count];
            try
            {
                return await ProbeSourceAsync(source, declaredFileSize, settings, connectionGate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                downloadFile.IncrementRetryCount();
            }

            if (attempt + 1 < attempts && (attempt + 1) % sources.Count == 0)
                await Task.Delay(CalculateRetryDelay((attempt + 1) / sources.Count), cancellationToken)
                    .ConfigureAwait(false);
        }

        throw new AggregateException("None of the download sources responded.", errors);
    }

    static async Task<ServerCapabilities> ProbeSourceAsync(
        string source,
        long declaredFileSize,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        CancellationToken cancellationToken)
    {
        await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = settings.HttpClientFactory.CreateClient(settings.HttpClientName ?? DefaultDownloadClientName);
            using var request = CreateRequest(HttpMethod.Get, source, settings);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            using var response = await SendForHeadersAsync(client, request, settings, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable &&
                response.Content.Headers.ContentRange?.Length == 0)
                return new ServerCapabilities(source, 0, false, stopwatch.Elapsed);

            if (!response.IsSuccessStatusCode)
                throw CreateResponseException(response, source);

            var contentRange = response.Content.Headers.ContentRange;
            var supportsRanges = response.StatusCode == HttpStatusCode.PartialContent &&
                                 contentRange is { HasRange: true, From: 0, To: 0 };
            var fileLength = supportsRanges
                ? contentRange?.Length ?? declaredFileSize
                : response.Content.Headers.ContentLength ?? declaredFileSize;

            return new ServerCapabilities(source, Math.Max(0, fileLength), supportsRanges, stopwatch.Elapsed);
        }
        finally
        {
            connectionGate.Release();
        }
    }

    static int CalculateAdaptivePartCount(long fileLength, TimeSpan responseTime, DownloadSettings settings)
    {
        var desired = fileLength switch
        {
            < 4L * 1024 * 1024 => 1,
            < 16L * 1024 * 1024 => 2,
            < 64L * 1024 * 1024 => 4,
            < 256L * 1024 * 1024 => 8,
            _ => 16
        };

        // A high first-byte latency usually means an overloaded or distant endpoint. Ramp up conservatively.
        if (responseTime >= TimeSpan.FromSeconds(2)) desired = Math.Min(desired, 2);
        else if (responseTime >= TimeSpan.FromMilliseconds(800)) desired = Math.Min(desired, 4);

        var configuredMaximum = settings.DownloadParts > 0 ? settings.DownloadParts : DefaultMaxParts;
        var maximum = Math.Clamp(configuredMaximum, 1, DefaultMaxParts);
        desired = Math.Min(desired, maximum);

        while (desired > 1 && fileLength / desired < MinimumChunkSize) desired /= 2;
        return Math.Max(1, desired);
    }

    static async Task DownloadWholeFileAsync(
        AbstractDownloadBase downloadFile,
        string tempPath,
        IReadOnlyList<string> sources,
        long expectedLength,
        int sourceOffset,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        DownloadProgressReporter progress,
        CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();
        var maxAttempts = GetMaxAttempts(settings);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sources[(sourceOffset + attempt) % sources.Count];

            try
            {
                await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var client = settings.HttpClientFactory.CreateClient(
                        settings.HttpClientName ?? DefaultDownloadClientName);
                    using var request = CreateRequest(HttpMethod.Get, source, settings);
                    using var response = await SendForHeadersAsync(client, request, settings, cancellationToken)
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) throw CreateResponseException(response, source);

                    var responseLength = response.Content.Headers.ContentLength ?? expectedLength;
                    if (expectedLength > 0 && responseLength > 0 && responseLength != expectedLength)
                        throw new InvalidDataException(
                            $"Source {source} reported {responseLength} bytes; expected {expectedLength}.");
                    if (responseLength > 0) progress.SetTotal(responseLength);
                    progress.Reset();

                    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read,
                        CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var buffer = MemoryPool<byte>.Shared.Rent(CopyBufferSize);
                    long received = 0;

                    while (true)
                    {
                        var read = await ReadWithStallTimeoutAsync(input, buffer.Memory, settings, cancellationToken)
                            .ConfigureAwait(false);
                        if (read == 0) break;
                        await output.WriteAsync(buffer.Memory[..read], cancellationToken).ConfigureAwait(false);
                        received += read;
                        progress.AddBytes(read);
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    if (responseLength > 0 && received != responseLength)
                        throw new IOException($"Incomplete download from {source}: expected {responseLength}, received {received}.");
                    return;
                }
                finally
                {
                    connectionGate.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                downloadFile.IncrementRetryCount();
            }

            if (attempt + 1 < maxAttempts)
                await Task.Delay(CalculateRetryDelay(attempt + 1), cancellationToken).ConfigureAwait(false);
        }

        throw new AggregateException("The complete-file download failed.", errors);
    }

    static async Task DownloadRangesAsync(
        AbstractDownloadBase downloadFile,
        string tempPath,
        long fileLength,
        int partCount,
        IReadOnlyList<string> sources,
        int sourceOffset,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        DownloadProgressReporter progress,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read,
            1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        output.SetLength(fileLength);

        var segments = CreateSegments(fileLength, partCount).ToArray();
        var channel = Channel.CreateUnbounded<DownloadSegment>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        foreach (var segment in segments) channel.Writer.TryWrite(segment);

        var remaining = segments.Length;
        var errors = new ConcurrentQueue<Exception>();
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var workerCount = Math.Min(partCount, segments.Length);
        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync(abort.Token).ConfigureAwait(false))
                while (channel.Reader.TryRead(out var segment))
                {
                    try
                    {
                        await DownloadSegmentAttemptAsync(output.SafeFileHandle, fileLength, segment, sources, sourceOffset,
                                settings, connectionGate, progress, abort.Token)
                            .ConfigureAwait(false);

                        if (Interlocked.Decrement(ref remaining) == 0)
                            channel.Writer.TryComplete();
                    }
                    catch (OperationCanceledException) when (abort.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        segment.Attempts++;
                        downloadFile.IncrementRetryCount();
                        if (segment.Attempts >= GetMaxAttempts(settings))
                        {
                            errors.Enqueue(ex);
                            channel.Writer.TryComplete(ex);
                            await abort.CancelAsync().ConfigureAwait(false);
                            throw;
                        }

                        await Task.Delay(CalculateRetryDelay(segment.Attempts), abort.Token).ConfigureAwait(false);
                        await channel.Writer.WriteAsync(segment, abort.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (abort.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Another worker has already recorded the terminal error.
            }
        }, CancellationToken.None)).ToArray();

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (errors.IsEmpty) errors.Enqueue(ex);
        }

        if (!errors.IsEmpty || Volatile.Read(ref remaining) != 0)
            throw new AggregateException("One or more download segments failed.", errors);

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    static IEnumerable<DownloadSegment> CreateSegments(long fileLength, int partCount)
    {
        var size = fileLength / partCount;
        var remainder = fileLength % partCount;
        long start = 0;

        for (var i = 0; i < partCount; i++)
        {
            var length = size + (i < remainder ? 1 : 0);
            yield return new DownloadSegment(start, start + length - 1);
            start += length;
        }
    }

    static async Task DownloadSegmentAttemptAsync(
        SafeFileHandle output,
        long fileLength,
        DownloadSegment segment,
        IReadOnlyList<string> sources,
        int sourceOffset,
        DownloadSettings settings,
        SemaphoreSlim connectionGate,
        DownloadProgressReporter progress,
        CancellationToken cancellationToken)
    {
        if (segment.NextOffset > segment.End) return;
        var source = sources[(sourceOffset + segment.Attempts) % sources.Count];
        await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var client = settings.HttpClientFactory.CreateClient(settings.HttpClientName ?? DefaultDownloadClientName);
            using var request = CreateRequest(HttpMethod.Get, source, settings);
            request.Headers.Range = new RangeHeaderValue(segment.NextOffset, segment.End);
            using var response = await SendForHeadersAsync(client, request, settings, cancellationToken)
                .ConfigureAwait(false);

            if (!IsValidRangeResponse(response, segment.NextOffset, segment.End, fileLength))
                throw CreateResponseException(response, source,
                    $"Source did not honor range {segment.NextOffset}-{segment.End}");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var buffer = MemoryPool<byte>.Shared.Rent(CopyBufferSize);

            while (segment.NextOffset <= segment.End)
            {
                var maximumRead = (int)Math.Min(buffer.Memory.Length, segment.End - segment.NextOffset + 1);
                var read = await ReadWithStallTimeoutAsync(input, buffer.Memory[..maximumRead], settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    throw new IOException($"Range {segment.Start}-{segment.End} ended at {segment.NextOffset}.");

                await RandomAccess.WriteAsync(output, buffer.Memory[..read], segment.NextOffset, cancellationToken)
                    .ConfigureAwait(false);
                segment.NextOffset += read;
                progress.AddBytes(read);
            }
        }
        finally
        {
            connectionGate.Release();
        }
    }

    static bool IsValidRangeResponse(HttpResponseMessage response, long start, long end, long fileLength)
    {
        var range = response.Content.Headers.ContentRange;
        return response.StatusCode == HttpStatusCode.PartialContent &&
               range is { HasRange: true } &&
               range.From == start &&
               range.To == end &&
               range.Length == fileLength &&
               response.Content.Headers.ContentLength == end - start + 1;
    }

    static HttpRequestMessage CreateRequest(HttpMethod method, string source, DownloadSettings settings)
    {
        var request = new HttpRequestMessage(method, source);
        if (settings.Authentication != null) request.Headers.Authorization = settings.Authentication;
        if (!string.IsNullOrWhiteSpace(settings.Host)) request.Headers.Host = settings.Host;
        return request;
    }

    static async Task<HttpResponseMessage> SendForHeadersAsync(
        HttpClient client,
        HttpRequestMessage request,
        DownloadSettings settings,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var configuredTimeout = NormalizeTimeout(settings.ConnectionTimeout, TimeSpan.FromSeconds(8));
        if (settings.Timeout > TimeSpan.Zero && settings.Timeout != Timeout.InfiniteTimeSpan)
            configuredTimeout = configuredTimeout < settings.Timeout ? configuredTimeout : settings.Timeout;
        timeout.CancelAfter(configuredTimeout);
        try
        {
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            timeout.CancelAfter(Timeout.InfiniteTimeSpan);
            return response;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for response headers from {request.RequestUri}.", ex);
        }
    }

    static async ValueTask<int> ReadWithStallTimeoutAsync(
        Stream stream,
        Memory<byte> buffer,
        DownloadSettings settings,
        CancellationToken cancellationToken)
    {
        var stallTimeout = NormalizeTimeout(settings.StallTimeout, TimeSpan.FromSeconds(12));
        if (settings.Timeout > TimeSpan.Zero && settings.Timeout != Timeout.InfiniteTimeSpan)
            stallTimeout = stallTimeout < settings.Timeout ? stallTimeout : settings.Timeout;
        try
        {
            return await stream.ReadAsync(buffer, cancellationToken).AsTask()
                .WaitAsync(stallTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"No download data was received for {stallTimeout.TotalSeconds:F0} seconds.", ex);
        }
    }

    static TimeSpan NormalizeTimeout(TimeSpan value, TimeSpan fallback)
    {
        if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan) return fallback;
        return value;
    }

    static int GetMaxAttempts(DownloadSettings settings)
    {
        return settings.RetryCount > 0 ? Math.Clamp(settings.RetryCount, 1, 32) : 4;
    }

    static HttpRequestException CreateResponseException(
        HttpResponseMessage response,
        string source,
        string? detail = null)
    {
        var message = detail == null
            ? $"Download source {source} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
            : $"{detail}; {source} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
        return new HttpRequestException(message, null, response.StatusCode);
    }

    static async Task VerifyFileAsync(
        string tempPath,
        AbstractDownloadBase downloadFile,
        DownloadSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.CheckFile || string.IsNullOrWhiteSpace(downloadFile.CheckSum)) return;

        using var hash = settings.GetCryptoTransform();
        await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = Convert.ToHexString(await hash.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!actual.Equals(downloadFile.CheckSum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Hash mismatch: expected {downloadFile.CheckSum}, got {actual}.");
    }

    static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Cleanup must not hide the original download error.
        }
    }

    sealed record ServerCapabilities(string Url, long FileLength, bool SupportsRanges, TimeSpan ResponseTime);

    sealed class DownloadSegment(long start, long end)
    {
        public long Start { get; } = start;
        public long End { get; } = end;
        public long NextOffset { get; set; } = start;
        public int Attempts { get; set; }
    }

    sealed class DownloadProgressReporter : IAsyncDisposable
    {
        readonly Action<double, long, long, bool> _callback;
        readonly CancellationTokenSource _stop = new();
        readonly Task _reportTask;
        readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        readonly TimeSpan _interval;
        long _bytes;
        long _total;
        int _completed;
        double _speed;

        public DownloadProgressReporter(
            long total,
            TimeSpan interval,
            Action<double, long, long, bool> callback)
        {
            this._total = total;
            this._interval = interval < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : interval;
            this._callback = callback;
            this.SafeReport(0, 0, total, false);
            this._reportTask = Task.Run(this.ReportLoopAsync);
        }

        public void AddBytes(int bytes)
        {
            Interlocked.Add(ref this._bytes, bytes);
        }

        public void SetTotal(long total)
        {
            if (total > 0) Interlocked.Exchange(ref this._total, total);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref this._bytes, 0);
            this._speed = 0;
        }

        public async ValueTask<double> CompleteAsync(bool succeeded)
        {
            if (Interlocked.Exchange(ref this._completed, 1) != 0)
                return this.GetAverageSpeed();

            await this._stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await this._reportTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the reporter.
            }

            var bytes = Interlocked.Read(ref this._bytes);
            var total = Interlocked.Read(ref this._total);
            if (succeeded && total > 0) bytes = total;
            this.SafeReport(succeeded ? this.GetAverageSpeed() : 0, bytes, total, succeeded);
            return this.GetAverageSpeed();
        }

        public async ValueTask DisposeAsync()
        {
            await this.CompleteAsync(false).ConfigureAwait(false);
            this._stop.Dispose();
        }

        async Task ReportLoopAsync()
        {
            var previousBytes = Interlocked.Read(ref this._bytes);
            var previousTimestamp = Stopwatch.GetTimestamp();

            while (true)
            {
                await Task.Delay(this._interval, this._stop.Token).ConfigureAwait(false);
                var now = Stopwatch.GetTimestamp();
                var bytes = Interlocked.Read(ref this._bytes);
                var elapsed = (double)(now - previousTimestamp) / Stopwatch.Frequency;
                var instantaneousSpeed = elapsed > 0 ? Math.Max(0, bytes - previousBytes) / elapsed : 0;
                this._speed = this._speed <= 0 ? instantaneousSpeed : this._speed * 0.65 + instantaneousSpeed * 0.35;
                previousBytes = bytes;
                previousTimestamp = now;
                this.SafeReport(this._speed, bytes, Interlocked.Read(ref this._total), false);
            }
        }

        double GetAverageSpeed()
        {
            return this._stopwatch.Elapsed.TotalSeconds > 0
                ? Interlocked.Read(ref this._bytes) / this._stopwatch.Elapsed.TotalSeconds
                : 0;
        }

        void SafeReport(double speed, long bytes, long total, bool finished)
        {
            try
            {
                this._callback(speed, bytes, total, finished);
            }
            catch
            {
                // A UI/event subscriber must not be able to corrupt the transfer state.
            }
        }
    }
}
