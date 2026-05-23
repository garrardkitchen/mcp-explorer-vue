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
}
