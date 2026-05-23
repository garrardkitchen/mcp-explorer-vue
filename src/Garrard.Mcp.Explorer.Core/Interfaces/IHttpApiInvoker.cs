using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Invokes an HTTP endpoint with the correct auth scheme and returns the raw response details.
/// </summary>
public interface IHttpApiInvoker
{
    Task<HttpApiInvokeResult> InvokeAsync(HttpApiDefinition definition, CancellationToken ct = default);
}

public sealed class HttpApiInvokeResult
{
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = [];
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
