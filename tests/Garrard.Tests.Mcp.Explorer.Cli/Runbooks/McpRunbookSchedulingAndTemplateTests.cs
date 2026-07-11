using Garrard.Mcp.Explorer.Cli.Runbooks;
using System.Text.Json;

namespace Garrard.Tests.Mcp.Explorer.Cli.Runbooks;

public class McpRunbookSchedulingAndTemplateTests
{
    [Fact]
    public void ComputeRunCount_UsesWindowAndRepeatCap()
    {
        var schedule = new RunbookSchedule
        {
            Repeat = 10,
            EverySeconds = 5,
            ForSeconds = 12
        };

        var runCount = RunbookSchedulePlanner.ComputeRunCount(schedule);

        Assert.Equal(3, runCount);
    }

    [Fact]
    public void ComputeRunCount_WindowWithoutRepeat_RunsFullWindow()
    {
        var schedule = new RunbookSchedule
        {
            EverySeconds = 30,
            ForSeconds = 300
        };

        var runCount = RunbookSchedulePlanner.ComputeRunCount(schedule);

        Assert.Equal(11, runCount);
    }

    [Fact]
    public void ComputeRunCount_NoScheduleConfigured_RunsOnce()
    {
        Assert.Equal(1, RunbookSchedulePlanner.ComputeRunCount(new RunbookSchedule()));
    }

    [Fact]
    public void ComputeRunCount_RepeatOnly_UsesRepeat()
    {
        Assert.Equal(5, RunbookSchedulePlanner.ComputeRunCount(new RunbookSchedule { Repeat = 5 }));
    }

    [Fact]
    public void ResolveObject_ReplacesStepResultTokens()
    {
        var resolver = new RunbookTemplateResolver();
        var previousResult = JsonSerializer.Deserialize<JsonElement>("""{"user":{"name":"Garrard"}}""");

        var parameters = new Dictionary<string, object?>
        {
            ["name"] = "{{ steps.first.user.name }}"
        };

        var resolved = (Dictionary<string, object?>)resolver.ResolveObject(
            parameters,
            new Dictionary<string, object?> { ["first"] = previousResult },
            iterationIndex: 0)!;

        Assert.Equal("Garrard", resolved["name"]?.ToString());
    }

    [Fact]
    public void ResolveObject_ReplacesIterationToken()
    {
        var resolver = new RunbookTemplateResolver();
        var value = resolver.ResolveObject("run-{{iteration.index}}", new Dictionary<string, object?>(), 4);

        Assert.Equal("run-4", value);
    }
}
