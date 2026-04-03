namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

public sealed record WorkflowDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? DefaultConnectionName { get; init; }
    public List<WorkflowStep> Steps { get; init; } = [];
    public List<string> HighlightedProperties { get; init; } = [];
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; init; } = DateTime.UtcNow;
}
