using System.Diagnostics;
using Iridium.Download;
using Iridium.Minecraft;

namespace Iridium.Sample;

/// <summary>
/// Ensures the resources (libraries / client jar / assets) referenced by a context exist
/// before launch, downloading any missing ones via the ResourceDownloader.
/// </summary>
internal static class ResourcePreparer {
    public static async Task EnsureAsync(MinecraftContext context, CancellationToken cancellationToken = default) {
        using var downloader = new ResourceDownloader(DownloadSource.Official, context.Layout);
        var stopwatch = Stopwatch.StartNew();

        downloader.ProgressChanged += (_, args) => {
            var speed = stopwatch.Elapsed.TotalSeconds > 0
                ? args.CompletedCount / stopwatch.Elapsed.TotalSeconds
                : 0;
            var current = args.CurrentFileName is { Length: > 0 } name ? $"  [{name}]" : string.Empty;
            Console.Write($"\r[{args.CompletedCount}/{args.TotalCount}] {args.Progress,6:P1}  {speed,6:F1} 文件/s{current}");
        };

        var result = await downloader.DownloadAsync(context.Entry, cancellationToken);
        stopwatch.Stop();

        Console.WriteLine();
        if (result.FailCount > 0)
            Console.WriteLine($"资源补全: 成功 {result.SuccessCount} 个, 失败 {result.FailCount} 个");
        else if (result.SuccessCount > 0)
            Console.WriteLine($"资源补全完成: {result.SuccessCount} 个文件, 用时 {stopwatch.Elapsed.TotalSeconds:F1}s");
        else
            Console.WriteLine("资源检查: 无需补全");
    }
}
