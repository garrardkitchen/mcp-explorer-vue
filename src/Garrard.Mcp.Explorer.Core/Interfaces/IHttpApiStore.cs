using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// CRUD store for HTTP API definitions, collections, groups, and favourites.
/// Config is persisted in HttpApis/http-apis.json (separate from settings.json).
/// </summary>
public interface IHttpApiStore
{
    // ── Definitions ──────────────────────────────────────────────────────────
    Task<IReadOnlyList<HttpApiDefinition>> GetAllDefinitionsAsync(CancellationToken ct = default);
    Task<HttpApiDefinition?> GetDefinitionAsync(string id, CancellationToken ct = default);
    Task<HttpApiDefinition> SaveDefinitionAsync(HttpApiDefinition definition, CancellationToken ct = default);
    Task DeleteDefinitionAsync(string id, CancellationToken ct = default);

    // ── Collections ──────────────────────────────────────────────────────────
    Task<IReadOnlyList<HttpApiCollection>> GetAllCollectionsAsync(CancellationToken ct = default);
    Task<HttpApiCollection?> GetCollectionAsync(string id, CancellationToken ct = default);
    Task<HttpApiCollection> SaveCollectionAsync(HttpApiCollection collection, CancellationToken ct = default);
    Task DeleteCollectionAsync(string id, CancellationToken ct = default);
    /// <summary>
    /// Updates only run-related stats on an existing collection without touching <c>LastUpdatedAt</c>.
    /// </summary>
    Task PatchCollectionRunStatsAsync(
        string id,
        DateTime lastRunAt,
        long durationMs,
        int successCount,
        int totalCount,
        string runId,
        string invokedVia,
        List<HttpApiCollectionEndpointRunSummary> endpointSummaries,
        CancellationToken ct = default);

    // ── Groups ───────────────────────────────────────────────────────────────
    Task<IReadOnlyList<HttpApiGroup>> GetAllGroupsAsync(CancellationToken ct = default);
    Task<HttpApiGroup> SaveGroupAsync(HttpApiGroup group, CancellationToken ct = default);
    Task DeleteGroupAsync(string name, CancellationToken ct = default);

    // ── Favourites ───────────────────────────────────────────────────────────
    Task<IReadOnlyList<string>> GetFavouriteIdsAsync(CancellationToken ct = default);
    Task SetFavouriteAsync(string id, bool isFavourite, CancellationToken ct = default);
}
