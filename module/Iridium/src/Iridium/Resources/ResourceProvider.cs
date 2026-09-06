using Iridium.Download;
using Iridium.Enums;
using Iridium.Models.Resources;
using Iridium.Resources.CurseForge;
using Iridium.Resources.Modrinth;

namespace Iridium.Resources;

/// <summary>
/// Aggregate search facade over both resource platforms. Queries Modrinth and
/// CurseForge in parallel and merges the results, deduplicating cross-platform
/// entries by slug / normalized title.
/// </summary>
public sealed class ResourceProvider {
    public ModrinthClient Modrinth { get; }
    public CurseForgeClient? CurseForge { get; }
    
    public ResourceProvider(ModrinthClient modrinth, CurseForgeClient curseForge) {
        ArgumentNullException.ThrowIfNull(modrinth);
        ArgumentNullException.ThrowIfNull(curseForge);
        
        Modrinth = modrinth;
        CurseForge = curseForge;
    }

    public ResourceProvider(ResourceApiSource? source = null, string? curseForgeApiKey = null) {
        Modrinth = new ModrinthClient(source);
        CurseForge = string.IsNullOrWhiteSpace(curseForgeApiKey)
            ? null
            : new CurseForgeClient(curseForgeApiKey, source);
    }

    public async Task<ResourceSearchPage<ResourceHit>> SearchAsync(ResourceSearchOptions options, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Source.HasFlag(ResourceSource.CurseForge) && CurseForge is null)
            throw new InvalidOperationException("CurseForge 未配置 API Key，无法搜索 CurseForge 资源。");

        Task<ResourceSearchPage<ResourceHit>>? modrinthTask = options.Source.HasFlag(ResourceSource.Modrinth)
            ? Modrinth.SearchAsync(options, cancellationToken)
            : null;
        
        Task<ResourceSearchPage<ResourceHit>>? curseForgeTask = options.Source.HasFlag(ResourceSource.CurseForge)
            ? CurseForge!.SearchAsync(options, cancellationToken)
            : null;

        if (modrinthTask is null && curseForgeTask is null)
            return new ResourceSearchPage<ResourceHit>([], 0, options.Page, options.PageSize);

        if (modrinthTask is not null && curseForgeTask is not null)
            await Task.WhenAll(modrinthTask, curseForgeTask);

        IReadOnlyList<ResourceHit> modrinthHits = [];
        IReadOnlyList<ResourceHit> curseForgeHits = [];
        var totalCount = 0;

        if (modrinthTask is not null) {
            var result = await modrinthTask;
            modrinthHits = result.Items;
            totalCount = result.TotalCount;
        }

        if (curseForgeTask is not null) {
            var result = await curseForgeTask;
            curseForgeHits = result.Items;
            totalCount = modrinthTask is null
                ? result.TotalCount
                : Math.Max(totalCount, result.TotalCount);
        }

        return new ResourceSearchPage<ResourceHit>(
            Merge(modrinthHits, curseForgeHits, options), totalCount, options.Page, options.PageSize);
    }

    public async Task<ResourceSearchPage<ResourceHit>> SearchAsync(Action<ResourceSearchOptionsBuilder> configure, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResourceSearchOptionsBuilder();
        configure(builder);
        return await SearchAsync(builder.Build(), cancellationToken);
    }

    private static List<ResourceHit> Merge(
        IReadOnlyList<ResourceHit> modrinthHits,
        IReadOnlyList<ResourceHit> curseForgeHits,
        ResourceSearchOptions options) {
        var ranked = modrinthHits.Select((hit, index) => new RankedHit(hit, index, modrinthHits.Count))
            .Concat(curseForgeHits.Select((hit, index) => new RankedHit(hit, index, curseForgeHits.Count)))
            .ToList();
        if (ranked.Count < 2)
            return ranked.Select(item => item.Hit).ToList();

        var maxDownloads = ranked.Max(item => item.Hit.Downloads);
        var maxModrinthFollows = modrinthHits.Count == 0 ? 0 : modrinthHits.Max(hit => hit.Follows);
        var maxCurseForgeDownloads = curseForgeHits.Count == 0 ? 0 : curseForgeHits.Max(hit => hit.Downloads);

        var ordered = options.Sort switch {
            ResourceSort.Downloads or ResourceSort.TotalDownloads =>
                ranked.OrderByDescending(item => item.Hit.Downloads),
            ResourceSort.Updated or ResourceSort.LastUpdated =>
                ranked.OrderByDescending(item => item.Hit.DateModified ?? DateTime.MinValue),
            ResourceSort.Newest or ResourceSort.ReleasedDate =>
                ranked.OrderByDescending(item => item.Hit.DateCreated ?? DateTime.MinValue),
            ResourceSort.Follows => ranked.OrderByDescending(item => item.Hit.Source == ResourceSource.Modrinth
                ? Normalize(item.Hit.Follows, maxModrinthFollows)
                : Normalize(item.Hit.Downloads, maxCurseForgeDownloads)),
            _ => ranked.OrderByDescending(item => RelevanceScore(item, maxDownloads,
                !string.IsNullOrWhiteSpace(options.Query)))
        };

        var merged = new List<ResourceHit>(ranked.Count);
        foreach (var item in ordered
                     .ThenBy(item => item.Hit.Source)
                     .ThenBy(item => item.Hit.Id, StringComparer.Ordinal)) {
            if (!merged.Any(existing => IsSameProject(existing, item.Hit)))
                merged.Add(item.Hit);
        }

        return merged;
    }

    private static double RelevanceScore(RankedHit item, long maxDownloads, bool hasQuery) {
        var rankScore = item.Count <= 1 ? 1d : 1d - (double)item.Index / item.Count;
        var downloadScore = Normalize(Math.Log(1d + item.Hit.Downloads), Math.Log(1d + maxDownloads));
        return hasQuery ? rankScore * 0.82 + downloadScore * 0.18 : rankScore * 0.55 + downloadScore * 0.45;
    }

    private static double Normalize(double value, double maximum) => maximum <= 0 ? 0 : value / maximum;

    private static bool IsSameProject(ResourceHit left, ResourceHit right) {
        if (left.Source == right.Source)
            return false;

        if (!string.IsNullOrWhiteSpace(left.Slug) && !string.IsNullOrWhiteSpace(right.Slug) &&
            string.Equals(Normalize(left.Slug), Normalize(right.Slug), StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(left.Title) && !string.IsNullOrWhiteSpace(right.Title) &&
               string.Equals(Normalize(left.Title), Normalize(right.Title), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit));

    private readonly record struct RankedHit(ResourceHit Hit, int Index, int Count);
}
