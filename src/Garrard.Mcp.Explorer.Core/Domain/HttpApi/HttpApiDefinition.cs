namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public HttpApiAuthenticationMode AuthenticationMode { get; set; } = HttpApiAuthenticationMode.None;
    public List<HttpApiHeader> Headers { get; init; } = [];
    public List<HttpApiQueryParam> QueryParams { get; init; } = [];
    public string? BodyTemplate { get; set; }
    public string? GroupName { get; set; }
    public List<string> Tags { get; init; } = [];
    public string Note { get; set; } = string.Empty;
    // Auth options reuse the same patterns as MCP connections
    public HttpApiAzureCredentialsOptions? AzureCredentials { get; set; }
    public HttpApiApiKeyOptions? ApiKeyOptions { get; set; }
    public HttpApiBearerOptions? BearerOptions { get; set; }
    // Schema version pinning — null means "always use latest snapshot"
    public string? GoldenSnapshotId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? LastInvokedAt { get; set; }
    public int? LastStatusCode { get; set; }
}
