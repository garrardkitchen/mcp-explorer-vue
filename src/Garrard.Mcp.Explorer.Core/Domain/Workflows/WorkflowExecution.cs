namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed record WorkflowExecution
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; init; }
    public WorkflowExecutionStatus Status { get; init; }
    public List<WorkflowStepResult> StepResults { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration => CompletedUtc.HasValue ? CompletedUtc.Value - StartedUtc : TimeSpan.Zero;
}
