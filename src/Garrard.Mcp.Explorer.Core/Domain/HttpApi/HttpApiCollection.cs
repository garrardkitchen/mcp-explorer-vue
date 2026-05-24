namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

public sealed class HttpApiCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Ordered list of <see cref="HttpApiDefinition.Id"/> values to run in sequence.</summary>
    public List<string> EndpointIds { get; init; } = [];
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    // ── Last-run aggregate stats (populated after each run) ──────────────────
    public long? LastRunDurationMs { get; set; }
    public int? LastRunSuccessCount { get; set; }
    public int? LastRunTotalCount { get; set; }
    public string? LastRunId { get; set; }
    public string? LastRunInvokedVia { get; set; }
    public List<HttpApiCollectionEndpointRunSummary>? LastRunEndpointSummaries { get; set; }
}
