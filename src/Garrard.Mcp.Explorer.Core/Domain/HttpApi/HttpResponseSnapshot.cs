namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

/// <summary>
/// A saved snapshot of an HTTP endpoint response — acts as the bookmark/baseline
/// for schema comparison. Stored in HttpApis/{endpointId}/snapshots.jsonl.
/// </summary>
public sealed class HttpResponseSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EndpointId { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = [];
    /// <summary>Inferred JSON schema of the response body (shape only, no values).</summary>
    public Dictionary<string, object?> InferredSchema { get; init; } = [];
    /// <summary>Truncated raw body for inspection (max 4 KB stored).</summary>
    public string? RawBodyTruncated { get; set; }
    /// <summary>Content-type of the response body.</summary>
    public string? ContentType { get; set; }
    /// <summary>Human-readable label set by the user when bookmarking.</summary>
    public string? Label { get; set; }
    /// <summary>When true this snapshot is pinned as the "golden baseline".</summary>
    public bool IsGolden { get; set; }
}
