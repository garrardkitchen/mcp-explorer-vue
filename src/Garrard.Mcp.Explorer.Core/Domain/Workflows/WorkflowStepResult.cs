using System.Text.Json.Serialization;

namespace Garrard.Mcp.Explorer.Core.Domain.Workflows;

[JsonConverter(typeof(WorkflowStepResultJsonConverter))]
public sealed record WorkflowStepResult
{
    public int StepNumber { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public StepExecutionStatus Status { get; init; } = StepExecutionStatus.Pending;
    public DateTime? StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public TimeSpan Duration => CompletedUtc.HasValue && StartedUtc.HasValue
        ? CompletedUtc.Value - StartedUtc.Value
        : TimeSpan.Zero;
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }

    // Derived — not serialised, computed from Status
    [JsonIgnore]
    public bool Success => Status == StepExecutionStatus.Completed;

    // Runtime only — not persisted
    [JsonIgnore]
    public object? Result { get; init; }
}
