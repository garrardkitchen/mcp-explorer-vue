namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

/// <summary>
/// Persisted record of a single collection run.
/// Stored in HttpApiCollections/{collectionId}/history.jsonl.
/// </summary>
public sealed class HttpApiCollectionRunRecord
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public string CollectionId { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public int SuccessCount { get; set; }
    public int TotalCount { get; set; }
    public string? InvokedVia { get; set; }
    public List<HttpApiCollectionEndpointRunSummary> EndpointSummaries { get; set; } = [];
}
