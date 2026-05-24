namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

/// <summary>
/// Lightweight per-endpoint result snapshot captured at the end of a collection run.
/// Stored on the collection to power the expand view without a separate history lookup.
/// </summary>
public sealed class HttpApiCollectionEndpointRunSummary
{
    public string EndpointId { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public bool IsSuccess { get; set; }
    public bool Skipped { get; set; }
}
