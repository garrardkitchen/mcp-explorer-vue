using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Invokes an HTTP endpoint with the correct auth scheme and returns the raw response details.
/// </summary>
public interface IHttpApiInvoker
{
    Task<HttpApiInvokeResult> InvokeAsync(HttpApiDefinition definition, CancellationToken ct = default);
    Task<HttpApiInvokeResult> InvokeAsync(
        HttpApiDefinition definition,
        IReadOnlyDictionary<string, string?>? inputs,
        CancellationToken ct = default);
}

public sealed class HttpApiInvokeResult
{
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = [];
    public string? ContentType { get; set; }
    /// <summary>Full response body (up to 1 MB) for live display.</summary>
    public string? Body { get; set; }
    /// <summary>Body truncated to 4 KB for snapshot/history storage.</summary>
    public string? TruncatedBody { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
