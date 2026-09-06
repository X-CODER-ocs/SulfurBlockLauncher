using System.Runtime.CompilerServices;
using Iridium.Java;
using Iridium.Models.Java;

namespace Iridium.Java;

public sealed class JavaProvider {
    public async IAsyncEnumerable<JavaEntry> EnumerableJavaAsync(
        bool fullDiskSearch = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var searched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var java in FastJavaScanner.Scan()) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!searched.Add(java))
                continue;

            if (await JavaParser.GetJavaEntryAsync(java, cancellationToken) is { } entry)
                yield return entry;
        }

        if (!fullDiskSearch)
            yield break;

        await foreach (var java in FullDiskJavaScanner.ScanAsync(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!searched.Add(java))
                continue;

            if (await JavaParser.GetJavaEntryAsync(java, cancellationToken) is { } entry)
                yield return entry;
        }
    }

    public Task<JavaEntry?> GetJavaEntryAsync(string javaPath, CancellationToken cancellationToken = default) =>
        JavaParser.GetJavaEntryAsync(javaPath, cancellationToken);
}
