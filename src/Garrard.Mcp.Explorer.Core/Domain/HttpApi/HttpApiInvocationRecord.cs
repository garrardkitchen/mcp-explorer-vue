namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

/// <summary>
/// Lightweight record written for every HTTP endpoint invocation.
/// Stored in HttpApis/{endpointId}/history.jsonl for trending / degradation analysis.
/// </summary>
public sealed class HttpApiInvocationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EndpointId { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;
    public DateTime InvokedAt { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    /// <summary>SHA-256 hash of the inferred schema JSON — used to detect schema drift without storing full content.</summary>
    public string SchemaHash { get; set; } = string.Empty;
    /// <summary>Whether this invocation's schema matched the active snapshot baseline.</summary>
    public bool? SchemaMatchedSnapshot { get; set; }
    /// <summary>If run as part of a collection, the collection's run ID.</summary>
    public string? CollectionRunId { get; set; }
    public string? ErrorMessage { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestBaseUrl { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; set; } = [];
    public Dictionary<string, string> RequestQueryParams { get; set; } = [];
    public Dictionary<string, string> ResponseHeaders { get; set; } = [];
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    /// <summary>Whether the invocation was triggered via the web app or CLI. See <see cref="HttpApiInvocationSource"/>.</summary>
    public string? InvokedVia { get; set; }
}
