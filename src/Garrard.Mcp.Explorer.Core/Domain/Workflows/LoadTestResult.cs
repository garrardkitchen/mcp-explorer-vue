namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed record LoadTestResult
{
    public string WorkflowId { get; init; } = string.Empty;
    public DateTime StartedUtc { get; init; }
    public DateTime CompletedUtc { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public double RequestsPerSecond { get; init; }
    public double AverageResponseMs { get; init; }
    public double P50ResponseMs { get; init; }
    public double P90ResponseMs { get; init; }
    public double P99ResponseMs { get; init; }
    public double ErrorRate { get; init; }
}
