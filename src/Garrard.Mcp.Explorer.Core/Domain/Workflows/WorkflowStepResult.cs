namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed class WorkflowStepResult
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
}
