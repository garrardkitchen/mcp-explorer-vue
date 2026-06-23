namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class RunbookExecutionSummary
{
    public RunbookExecutionSummary(IReadOnlyDictionary<string, object?> results, int failureCount)
    {
        Results = results;
        FailureCount = failureCount;
    }

    public IReadOnlyDictionary<string, object?> Results { get; }
    public int FailureCount { get; }
}
