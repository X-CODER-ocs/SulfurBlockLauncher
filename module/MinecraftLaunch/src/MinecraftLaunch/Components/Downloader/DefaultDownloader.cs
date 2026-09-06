using Flurl.Http;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.EventArgs;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Utilities;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace MinecraftLaunch.Components.Downloader;

public class DefaultDownloader : IDownloader {
    private const int BufferSize = 4096;
    private const long SegmentThreshold = 1048576;
    private const double kilobyte = 1024.0;
    private const double megabyte = kilobyte * 1024.0;
    private const double gigabyte = megabyte * 1024.0;

    // Per-instance settings so callers can run isolated downloads without
    // touching the process-wide DownloadManager configuration.
    public int MaxThread { get; init; } = DownloadManager.MaxThread;
    public int MaxFragment { get; init; } = DownloadManager.MaxFragment;
    public int MaxRetryCount { get; init; } = DownloadManager.MaxRetryCount;
    public bool IsEnableFragment { get; init; } = DownloadManager.IsEnableFragment;

    public static string FormatSize(double bytes, bool includePerSecond = false) {
        string suffix;
        if (bytes < kilobyte)
            suffix = "B";
        else if (bytes < megabyte) {
            bytes /= kilobyte;
            suffix = "KB";
        } else if (bytes < gigabyte) {
            bytes /= megabyte;
            suffix = "MB";
        } else {
            bytes /= gigabyte;
            suffix = "GB";
        }

        string format = bytes < 100 ? "0.00" : "0.0";
        string result = bytes.ToString(format) + " " + suffix;
        return includePerSecond ? result + "/s" : result;
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default) {
        var maxAttempts = Math.Max(1, MaxRetryCount);
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            try {
                await DownloadFileDriverAsync(request, cancellationToken);
                request.Completed?.Invoke(EventArgs.Empty);
                return new DownloadResult(DownloadResultType.Successful);
            } catch (OperationCanceledException) {
                return new DownloadResult(DownloadResultType.Cancelled);
            } catch (Exception ex) {
                if (attempt == maxAttempts - 1)
                    return new DownloadResult(DownloadResultType.Failed) { Exception = ex };

                await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        return new DownloadResult(DownloadResultType.Failed);
    }

    public async Task<GroupDownloadResult> DownloadManyAsync(GroupDownloadRequest requests, CancellationToken cancellationToken = default) {
        var downloadStates = new GroupDownloadStates {
            DownloadedCount = 0,
            TotalCount = requests.Files.Count(),
            TotalBytes = requests.Files.Sum(x => x.Size)
        };

        _ = ReportGroupProgressAsync(downloadStates, requests, cancellationToken);
        // Do not create one task per file. Large installations can contain thousands of files;
        // cancelling all of them at once starves rendering even when progress updates are throttled.
        await Parallel.ForEachAsync(requests.Files, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, MaxThread),
            CancellationToken = cancellationToken
        }, async (request, token) => await DownloadInGroupAsync(downloadStates, request, token));

        var type = downloadStates.FailedRequests.IsEmpty ? DownloadResultType.Successful : DownloadResultType.Failed;

        requests.ProgressChanged?.Invoke(new ResourceDownloadProgressChangedEventArgs {
            Speed = 0,
            TotalBytes = downloadStates.TotalBytes,
            EstimatedRemaining = TimeSpan.Zero,
            DownloadedBytes = downloadStates.States.Sum(x => x.DownloadedBytes),
            TotalCount = downloadStates.TotalCount,
            CompletedCount = downloadStates.DownloadedCount
        });

        return new GroupDownloadResult {
            Failed = downloadStates.FailedRequests,
            Type = type
        };
    }

    private async Task DownloadInGroupAsync(GroupDownloadStates downloadStates, DownloadRequest request, CancellationToken cancellationToken = default) {
        if (!request.FileInfo.Directory.Exists)
            request.FileInfo.Directory.Create();

        var maxAttempts = Math.Max(1, MaxRetryCount);
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            try {
                string url = request.Url;
                var (response, finalUrl) = await PrepareForDownloadAsync(url, cancellationToken).ConfigureAwait(false);

                var states = new DownloadStates {
                    Url = finalUrl,
                    FragmentSize = SegmentThreshold,
                    Stopwatch = Stopwatch.StartNew(),
                    LocalPath = request.FileInfo.FullName,
                };

                downloadStates.States.Add(states);
                if (response.Content.Headers.ContentLength is long contentLength)
                    states.TotalBytes = contentLength;
                else
                    states.TotalBytes = request.Size;
                response.Dispose();

                if (IsEnableFragment && states.TotalBytes > 0) {
                    bool supportsRange = await ValidateRangeSupport(finalUrl, cancellationToken).ConfigureAwait(false);
                    if (supportsRange) {
                        await DownloadMultiPartAsync(states, request, cancellationToken).ConfigureAwait(false);
                        Interlocked.Increment(ref downloadStates.DownloadedCount);
                        return;
                    }
                }

                await DownloadSinglePartAsync(states, request, cancellationToken).ConfigureAwait(false);
                DownloadManager.RecordTransferSuccess(states.Url, states.DownloadedBytes, states.Stopwatch.Elapsed);
                Interlocked.Increment(ref downloadStates.DownloadedCount);
                request.Completed?.Invoke(EventArgs.Empty);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (OperationCanceledException) {
            } catch (Exception) {
                if (attempt + 1 < maxAttempts)
                    await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }
        downloadStates.FailedRequests.Add(request);
    }

    private static async Task<bool> ValidateRangeSupport(string url, CancellationToken cancellationToken) {
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
        rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await HttpUtil.DownloaderClient.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        return response.StatusCode == HttpStatusCode.PartialContent;
    }

    private async Task DownloadFileDriverAsync(DownloadRequest request, CancellationToken cancellationToken) {
        string url = request.Url;
        var (response, finalUrl) = await PrepareForDownloadAsync(url, cancellationToken).ConfigureAwait(false);

        var states = new DownloadStates {
            Url = finalUrl,
            FragmentSize = SegmentThreshold,
            Stopwatch = Stopwatch.StartNew(),
            LocalPath = request.FileInfo.FullName
        };

        if (!request.FileInfo.Directory.Exists)
            request.FileInfo.Directory.Create();

        if (response.Content.Headers.ContentLength is long contentLength)
            states.TotalBytes = contentLength;
        else
            states.TotalBytes = request.Size;
        response.Dispose();

        var progressTask = ReportProgressAsync(states, request, cancellationToken);
        try {
            if (IsEnableFragment && states.TotalBytes > 0) {
                bool supportsRange = await ValidateRangeSupport(finalUrl, cancellationToken);
                if (supportsRange) {
                    await DownloadMultiPartAsync(states, request, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            await DownloadSinglePartAsync(states, request, cancellationToken).ConfigureAwait(false);
            DownloadManager.RecordTransferSuccess(states.Url, states.DownloadedBytes, states.Stopwatch.Elapsed);
        } finally {
            Interlocked.Exchange(ref states.IsCompleted, 1);
            await progressTask.ConfigureAwait(false);
        }
    }

    private static async Task<(HttpResponseMessage, string)> PrepareForDownloadAsync(string url, CancellationToken cancellationToken) {
        const int maxRedirects = 10;
        var currentUrl = url;

        for (var redirectCount = 0; redirectCount <= maxRedirects; redirectCount++) {
            var response = await HttpUtil.FlurlClient.Request(currentUrl)
                .AllowAnyHttpStatus()
                .HeadAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var message = response.ResponseMessage;

            if ((int)message.StatusCode is >= 300 and < 400 && message.Headers.Location is { } location) {
                if (redirectCount == maxRedirects) {
                    message.Dispose();
                    throw new HttpRequestException($"Too many redirects while downloading {url}");
                }

                var nextUrl = location.IsAbsoluteUri
                    ? location.AbsoluteUri
                    : new Uri(new Uri(currentUrl), location).AbsoluteUri;
                message.Dispose();
                currentUrl = nextUrl;
                continue;
            }

            message.EnsureSuccessStatusCode();
            return (message, message.RequestMessage?.RequestUri?.AbsoluteUri ?? currentUrl);
        }

        throw new HttpRequestException($"Too many redirects while downloading {url}");
    }

    private async Task DownloadMultiPartAsync(DownloadStates states, DownloadRequest request, CancellationToken cancellationToken) {
        long fileSize = states.TotalBytes;
        long totalSegments = (fileSize + SegmentThreshold - 1) / SegmentThreshold;
        states.TotalFragments = totalSegments;

        await using var fileStream = new FileStream(states.LocalPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, BufferSize, true);
        fileStream.SetLength(fileSize);

        var tasks = new List<Task>();
        int workers = Math.Min(Math.Max(1, MaxFragment), (int)totalSegments);
        for (int i = 0; i < workers; i++)
            tasks.Add(MultipartDownloadWorker(states, request, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task DownloadSinglePartAsync(DownloadStates states, DownloadRequest request, CancellationToken cancellationToken) {
        using var response = await HttpUtil.DownloaderClient.GetAsync(states.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        states.Url = response.RequestMessage?.RequestUri?.AbsoluteUri ?? states.Url;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(states.LocalPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, BufferSize, true);

        if (states.TotalBytes is long size and > 0)
            fileStream.SetLength(size);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try {
            await WriteStreamToFile(contentStream, fileStream, buffer, request, states, cancellationToken).ConfigureAwait(false);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

    }

    private static async Task MultipartDownloadWorker(DownloadStates states, DownloadRequest request, CancellationToken cancellationToken) {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try {
            while (states.NextFragment() is (long start, long end)) {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, states.Url);
                httpRequest.Headers.Range = new RangeHeaderValue(start, end);
                var started = Stopwatch.GetTimestamp();
                using var response = await HttpUtil.FlurlClient.HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var range = response.Content.Headers.ContentRange;
                if (response.StatusCode != HttpStatusCode.PartialContent || range?.From != start || range.To != end)
                    throw new HttpRequestException($"Invalid range response for bytes {start}-{end}.");

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(states.LocalPath, FileMode.Open, FileAccess.Write, FileShare.Write);
                fileStream.Seek(start, SeekOrigin.Begin);
                var before = Interlocked.Read(ref states.DownloadedBytes);
                await WriteStreamToFile(contentStream, fileStream, buffer, request, states, cancellationToken).ConfigureAwait(false);
                var actualUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? states.Url;
                DownloadManager.RecordTransferSuccess(actualUrl,
                    Interlocked.Read(ref states.DownloadedBytes) - before, Stopwatch.GetElapsedTime(started));
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task ReportProgressAsync(DownloadStates states, DownloadRequest request, CancellationToken cancellationToken) {
        var sw = Stopwatch.StartNew();
        long prevBytes = 0;
        double prevTime = 0;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
            long nowBytes = Interlocked.Read(ref states.DownloadedBytes);
            long totalBytes = Interlocked.Read(ref states.TotalBytes);

            if (Volatile.Read(ref states.IsCompleted) != 0 || totalBytes > 0 && nowBytes >= totalBytes)
                break;

            double nowTime = sw.Elapsed.TotalSeconds;
            double deltaB = nowBytes - prevBytes;
            double deltaT = nowTime - prevTime;
            long speed = deltaT > 0 ? (long)(deltaB / deltaT) : 0;

            TimeSpan eta = speed > 0
                ? TimeSpan.FromSeconds((totalBytes - nowBytes) / (double)speed)
                : TimeSpan.Zero;

            prevTime = nowTime;
            prevBytes = nowBytes;

            request.ProgressChanged?.Invoke(new ResourceDownloadProgressChangedEventArgs {
                Speed = speed,
                EstimatedRemaining = eta,
                TotalBytes = totalBytes,
                DownloadedBytes = nowBytes,
                TotalCount = 1,
                CompletedCount = 0
            });
        }

        request.ProgressChanged?.Invoke(new ResourceDownloadProgressChangedEventArgs {
            Speed = 0,
            TotalBytes = states.TotalBytes,
            EstimatedRemaining = TimeSpan.Zero,
            DownloadedBytes = states.DownloadedBytes,
            TotalCount = 1,
            CompletedCount = 1
        });
    }

    private static async Task ReportGroupProgressAsync(GroupDownloadStates states, GroupDownloadRequest request, CancellationToken cancellationToken) {
        var sw = Stopwatch.StartNew();
        long prevBytes = 0;
        double prevTime = 0;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
            Interlocked.Exchange(ref states.TotalBytes, states.States.Sum(x => x.TotalBytes));
            Interlocked.Exchange(ref states.DownloadedBytes, states.States.Sum(x => x.DownloadedBytes));

            var totalBytes = Interlocked.Read(ref states.TotalBytes);
            var nowBytes = Interlocked.Read(ref states.DownloadedBytes);

            if (states.TotalCount is 0)
                break;

            if (states.TotalBytes is not 0 && states.TotalCount == states.DownloadedCount)
                break;

            if (totalBytes > 0 && nowBytes >= totalBytes)
                break;

            double nowTime = sw.Elapsed.TotalSeconds;
            double deltaB = nowBytes - prevBytes;
            double deltaT = nowTime - prevTime;
            long speed = deltaT > 0 ? (long)(deltaB / deltaT) : 0;

            TimeSpan eta = speed > 0
                ? TimeSpan.FromSeconds((totalBytes - nowBytes) / (double)speed)
                : TimeSpan.Zero;

            prevTime = nowTime;
            prevBytes = nowBytes;

            request.ProgressChanged?.Invoke(new ResourceDownloadProgressChangedEventArgs {
                Speed = speed,
                EstimatedRemaining = eta,
                TotalBytes = totalBytes,
                DownloadedBytes = nowBytes,
                TotalCount = states.TotalCount,
                CompletedCount = states.DownloadedCount
            });
        }
    }

    private static async Task WriteStreamToFile(Stream contentStream, FileStream fileStream, byte[] buffer, DownloadRequest request, DownloadStates states, CancellationToken cancellationToken) {
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            Interlocked.Add(ref states.DownloadedBytes, bytesRead);
        }
    }

    private class DownloadStates {
        private long _fragmentScheduled = -1;

        public long TotalBytes;
        public long DownloadedBytes;
        public int IsCompleted;

        public long FragmentSize;
        public long TotalFragments;

        public string Url;
        public string LocalPath;

        public Stopwatch Stopwatch = new();

        public (long start, long end)? NextFragment() {
            long index = Interlocked.Increment(ref _fragmentScheduled);
            if (index >= TotalFragments)
                return null;

            long start = index * FragmentSize;
            long end = Math.Min(start + FragmentSize, TotalBytes) - 1;
            return (start, end);
        }
    }

    private class GroupDownloadStates {
        public long TotalBytes;
        public long DownloadedBytes;

        public int TotalCount;
        public int DownloadedCount;

        public ConcurrentBag<DownloadStates> States = [];
        public ConcurrentBag<DownloadRequest> FailedRequests = [];
    }
}
