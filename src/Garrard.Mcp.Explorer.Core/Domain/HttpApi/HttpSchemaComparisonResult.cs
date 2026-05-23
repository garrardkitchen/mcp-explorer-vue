namespace Garrard.Mcp.Explorer.Core.Domain.HttpApi;

/// <summary>
/// Result of comparing a live HTTP response schema against a saved snapshot baseline.
/// </summary>
public sealed class HttpSchemaComparisonResult
{
    public string EndpointId { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;
    public string? SnapshotId { get; set; }
    public DateTime ComparedAt { get; set; } = DateTime.UtcNow;
    public int LiveStatusCode { get; set; }
    public int SnapshotStatusCode { get; set; }
    public long LiveLatencyMs { get; set; }
    public long SnapshotLatencyMs { get; set; }
    public List<string> AddedProperties { get; init; } = [];
    public List<string> RemovedProperties { get; init; } = [];
    public List<SchemaPropertyChange> ChangedTypes { get; init; } = [];
    public bool StatusCodeChanged => LiveStatusCode != SnapshotStatusCode;
    /// <summary>True when any removed property or type change was detected, or the status code changed.</summary>
    public bool IsBreaking => RemovedProperties.Count > 0 || ChangedTypes.Count > 0 || StatusCodeChanged;
    /// <summary>True when latency increased by more than the degradation threshold (default 2×).</summary>
    public bool IsDegraded { get; set; }
    public double LatencyRatio => SnapshotLatencyMs > 0 ? (double)LiveLatencyMs / SnapshotLatencyMs : 1.0;
}

public sealed class SchemaPropertyChange
{
    public string PropertyPath { get; set; } = string.Empty;
    public string PreviousType { get; set; } = string.Empty;
    public string CurrentType { get; set; } = string.Empty;
}
