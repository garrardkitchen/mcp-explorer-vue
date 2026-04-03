namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed class WorkflowStep
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public List<ParameterMapping> ParameterMappings { get; set; } = [];
    public ErrorHandlingMode ErrorHandling { get; set; } = ErrorHandlingMode.StopOnError;
    public string? Notes { get; set; }
}
