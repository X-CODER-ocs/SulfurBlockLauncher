using System.Text.Json;
using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Models.Resources;

using ModrinthSearchResultContext = Iridium.Resources.Modrinth.ModrinthSearchResultContext;

namespace Iridium.Resources.Modrinth;

internal static class ModrinthRequestBuilder {
    public static string BuildFacets(ResourceSearchOptions options) {
        var facets = new List<List<string>>(options.Tags.Count + options.ExcludedTags.Count + 5) {
            new() { $"project_type:{options.Type.ToModrinthProjectType()}" }
        };

        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            facets.Add([$"versions:'{options.GameVersion}'"]);

        facets.AddRange(options.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.ModrinthSlug))
            .Select(tag => (List<string>)[$"categories:'{tag.ModrinthSlug}'"]));

        facets.AddRange(options.ExcludedTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.ModrinthSlug))
            .Select(tag => (List<string>)[$"categories!='{tag.ModrinthSlug}'"]));

        if (options.Loader.ToModrinthLoader() is { } loader)
            facets.Add([$"categories:'{loader}'"]);

        if (options.Environment == ResourceEnvironment.Client)
            facets.Add([
                "environment:'client_only'", "environment:'client_only_server_optional'",
                "environment:'singleplayer_only'", "environment:'client_and_server'",
                "environment:'client_or_server'", "environment:'client_or_server_prefers_both'"
            ]);
        if (options.Environment == ResourceEnvironment.Server)
            facets.Add([
                "environment:'server_only'", "environment:'server_only_client_optional'",
                "environment:'dedicated_server_only'", "environment:'client_and_server'",
                "environment:'client_or_server'", "environment:'client_or_server_prefers_both'"
            ]);
        if (options.Environment == ResourceEnvironment.ClientAndServer)
            facets.Add(["environment:'client_and_server'", "environment:'client_or_server_prefers_both'"]);

        return JsonSerializer.Serialize(facets,
            ModrinthSearchResultContext.Default.ListListString);
    }

    public static string ToAlgorithm(HashAlgorithm algorithm) => algorithm switch {
        HashAlgorithm.Sha1 => "sha1",
        HashAlgorithm.Sha512 => "sha512",
        _ => "sha1"
    };
}
