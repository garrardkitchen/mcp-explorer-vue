namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed record LoadTestSnapshot
{
    public double ElapsedMs { get; init; }
    public int CumulativeSuccesses { get; init; }
    public int CumulativeFailures { get; init; }
    public int ActiveExecutions { get; init; }
}

public sealed record LoadTestResult
{
    public string WorkflowId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }
    public int MaxParallelExecutions { get; init; }
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
    public List<LoadTestSnapshot> Snapshots { get; init; } = [];
}

public sealed record LoadTestProgress
{
    public string RunId { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
    public double PercentComplete { get; init; }
    public int TotalExecutions { get; init; }
    public int SuccessfulExecutions { get; init; }
    public int FailedExecutions { get; init; }
    public int ActiveExecutions { get; init; }
    public LoadTestResult? Result { get; init; }
}
