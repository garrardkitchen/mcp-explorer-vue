using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Persists HTTP response snapshots and lightweight invocation history.
/// Stored under HttpApis/{endpointId}/ — never in settings.json.
/// </summary>
public interface IHttpApiSnapshotStore
{
    // ── Snapshots (bookmarks) ─────────────────────────────────────────────────
    Task AppendSnapshotAsync(HttpResponseSnapshot snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<HttpResponseSnapshot>> GetSnapshotsAsync(string endpointId, CancellationToken ct = default);
    Task<HttpResponseSnapshot?> GetSnapshotAsync(string endpointId, string snapshotId, CancellationToken ct = default);
    Task DeleteSnapshotAsync(string endpointId, string snapshotId, CancellationToken ct = default);

    // ── Invocation history ───────────────────────────────────────────────────
    Task AppendInvocationAsync(HttpApiInvocationRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<HttpApiInvocationRecord>> GetHistoryAsync(string endpointId, int? limit = null, CancellationToken ct = default);
    Task<IReadOnlyList<HttpApiInvocationRecord>> GetGlobalHistoryAsync(int? limit = null, CancellationToken ct = default);
    /// <summary>Returns the most recent invocation record per endpoint, keyed by endpoint ID.</summary>
    Task<IReadOnlyDictionary<string, HttpApiInvocationRecord>> GetLatestStatusesAsync(CancellationToken ct = default);

    // ── Collection run history ────────────────────────────────────────────────
    Task AppendCollectionRunAsync(HttpApiCollectionRunRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<HttpApiCollectionRunRecord>> GetCollectionRunHistoryAsync(string collectionId, int? limit = null, CancellationToken ct = default);
    Task DeleteCollectionRunHistoryAsync(string collectionId, CancellationToken ct = default);

    // ── Sparkline batch reads ─────────────────────────────────────────────────
    /// <summary>Returns the last <paramref name="limit"/> invocation records per endpoint, keyed by endpoint ID, ordered oldest-first for chart rendering.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<HttpApiInvocationRecord>>> GetEndpointSparklineDataAsync(int limit = 10, CancellationToken ct = default);
    /// <summary>Returns the last <paramref name="limit"/> collection run records per collection, keyed by collection ID, ordered oldest-first for chart rendering.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<HttpApiCollectionRunRecord>>> GetCollectionSparklineDataAsync(int limit = 10, CancellationToken ct = default);
}
