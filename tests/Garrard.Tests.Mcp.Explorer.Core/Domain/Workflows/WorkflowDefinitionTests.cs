using Garrard.Mcp.Explorer.Core.Domain.Workflows;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.Workflows;

public class WorkflowDefinitionTests
{
    // ── WorkflowDefinition ────────────────────────────────────────────────────

    [Fact]
    public void DefaultConstruction_SetsExpectedDefaults()
    {
        var wf = new WorkflowDefinition();

        Assert.NotNull(wf.Id);
        Assert.NotEmpty(wf.Id);
        Assert.Equal(string.Empty, wf.Name);
        Assert.Equal(string.Empty, wf.Description);
        Assert.Null(wf.DefaultConnectionName);
        Assert.Empty(wf.Steps);
        Assert.Empty(wf.HighlightedProperties);
    }

    [Fact]
    public void CanConstructWithSteps()
    {
        var step = new WorkflowStep { StepNumber = 1, ToolName = "list_files" };
        var wf = new WorkflowDefinition
        {
            Name = "My Workflow",
            Steps = [step]
        };

        Assert.Equal("My Workflow", wf.Name);
        Assert.Single(wf.Steps);
        Assert.Equal("list_files", wf.Steps[0].ToolName);
    }

    [Fact]
    public void TwoWorkflows_WithSameId_HaveSameProperties()
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var wf1 = new WorkflowDefinition { Id = id, Name = "WF", CreatedUtc = now, ModifiedUtc = now };
        var wf2 = new WorkflowDefinition { Id = id, Name = "WF", CreatedUtc = now, ModifiedUtc = now };

        // Records with List<T> properties compare lists by reference, so we verify key fields individually
        Assert.Equal(wf1.Id, wf2.Id);
        Assert.Equal(wf1.Name, wf2.Name);
        Assert.Equal(wf1.CreatedUtc, wf2.CreatedUtc);
    }

    [Fact]
    public void UniqueIds_PerDefaultInstance()
    {
        var wf1 = new WorkflowDefinition();
        var wf2 = new WorkflowDefinition();

        Assert.NotEqual(wf1.Id, wf2.Id);
    }

    // ── ErrorHandlingMode ─────────────────────────────────────────────────────

    [Fact]
    public void ErrorHandlingMode_HasExpectedValues()
    {
        var values = Enum.GetValues<ErrorHandlingMode>();

        Assert.Contains(ErrorHandlingMode.StopOnError, values);
        Assert.Contains(ErrorHandlingMode.ContinueOnError, values);
    }

    [Fact]
    public void WorkflowStep_DefaultErrorHandling_IsStopOnError()
    {
        var step = new WorkflowStep();

        Assert.Equal(ErrorHandlingMode.StopOnError, step.ErrorHandling);
    }

    // ── ParameterMapping ──────────────────────────────────────────────────────

    [Fact]
    public void ParameterMapping_WithLiteralValue()
    {
        var mapping = new ParameterMapping
        {
            ParameterName = "city",
            Value = "London"
        };

        Assert.Equal("city", mapping.ParameterName);
        Assert.Equal("London", mapping.Value);
        Assert.Null(mapping.SourceStepNumber);
        Assert.Null(mapping.SourcePropertyName);
    }

    [Fact]
    public void ParameterMapping_WithSourceStepReference()
    {
        var mapping = new ParameterMapping
        {
            ParameterName = "fileList",
            SourceStepNumber = 1,
            SourcePropertyName = "result"
        };

        Assert.Equal(1, mapping.SourceStepNumber);
        Assert.Equal("result", mapping.SourcePropertyName);
        Assert.Null(mapping.Value);
    }

    // ── WorkflowExecution & status transitions ────────────────────────────────

    [Theory]
    [InlineData(WorkflowExecutionStatus.Running)]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.PartiallyCompleted)]
    public void WorkflowExecution_CanBeCreatedWithAnyStatus(WorkflowExecutionStatus status)
    {
        var exec = new WorkflowExecution { Status = status };

        Assert.Equal(status, exec.Status);
    }

    [Fact]
    public void WorkflowExecution_DurationIsZero_WhenNotCompleted()
    {
        var exec = new WorkflowExecution { StartedUtc = DateTime.UtcNow };

        Assert.Equal(TimeSpan.Zero, exec.Duration);
    }

    [Fact]
    public void WorkflowExecution_Duration_ReflectsTimeDifference()
    {
        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = start.AddSeconds(30);

        var exec = new WorkflowExecution
        {
            StartedUtc = start,
            CompletedUtc = end,
            Status = WorkflowExecutionStatus.Completed
        };

        Assert.Equal(TimeSpan.FromSeconds(30), exec.Duration);
    }

    [Fact]
    public void WorkflowExecution_DefaultConstruction_HasUniqueId()
    {
        var e1 = new WorkflowExecution();
        var e2 = new WorkflowExecution();

        Assert.NotEqual(e1.Id, e2.Id);
    }

    [Fact]
    public void WorkflowExecution_CanCarryErrorMessage()
    {
        var exec = new WorkflowExecution
        {
            Status = WorkflowExecutionStatus.Failed,
            ErrorMessage = "Tool not found"
        };

        Assert.Equal("Tool not found", exec.ErrorMessage);
    }
}
